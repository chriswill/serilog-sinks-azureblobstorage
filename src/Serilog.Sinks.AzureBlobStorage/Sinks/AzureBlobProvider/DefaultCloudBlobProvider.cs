// Copyright 2018 CloudScope, LLC
// Portions copyright 2014 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;


namespace Serilog.Sinks.AzureBlobStorage.AzureBlobProvider
{
    internal class DefaultCloudBlobProvider : ICloudBlobProvider
    {
        private CloudAppendBlob currentCloudAppendBlob;
        private string currentBlobName = string.Empty;
        private int currentBlobRollSequence = 0;

        private static readonly int MaxBlocksOnBlobBeforeRoll = 49500; //small margin to the practical max of 50k, in case of many multiple writers to the same blob

        public async Task<CloudAppendBlob> GetCloudBlobAsync(CloudBlobClient cloudBlobClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation, long? blobSizeLimitBytes = null)
        {
            // Check if the current known blob is the targeted blob
            if (currentCloudAppendBlob != null && currentBlobName.Equals(blobName, StringComparison.OrdinalIgnoreCase))
            {
                // Check if the current blob is within the block count and file size limits
                if(ValidateBlobProperties(currentCloudAppendBlob, blobSizeLimitBytes))
                {                    
                    return currentCloudAppendBlob;
                }
                else
                {
                    // The blob is correct but needs to be rolled over
                    currentBlobRollSequence++;
                    await GetCloudAppendBlobAsync(cloudBlobClient, blobContainerName, blobName, bypassBlobCreationValidation);
                }
            }
            else
            {
                //first time to get a cloudblob or the blobname has changed
                currentBlobRollSequence = 0;
                await GetCloudAppendBlobAsync(cloudBlobClient, blobContainerName, blobName, bypassBlobCreationValidation, blobSizeLimitBytes);
            }

            return currentCloudAppendBlob;
        }

        private async Task GetCloudAppendBlobAsync(CloudBlobClient cloudBlobClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation, long? blobSizeLimitBytes = null)
        {
            //try to get a reference to a cloudappendblob which is below the max blocks threshold.
            for (int i = currentBlobRollSequence; i < 999; i++)
            {
                string rolledBlobName = GetRolledBlobName(blobName, i);
                CloudAppendBlob newCloudAppendBlob = await GetBlobReferenceAsync(cloudBlobClient, blobContainerName, rolledBlobName, bypassBlobCreationValidation);
                if (ValidateBlobProperties(newCloudAppendBlob, blobSizeLimitBytes))
                {
                    currentCloudAppendBlob = newCloudAppendBlob;
                    currentBlobName = blobName;
                    currentBlobRollSequence = i;
                    break;
                }
            }
        }

        private bool ValidateBlobProperties(CloudAppendBlob blob, long? blobSizeLimitBytes = null)
        {
            if (blob == null)
                throw new ArgumentNullException(nameof(blob));

            return blob.Properties.AppendBlobCommittedBlockCount < MaxBlocksOnBlobBeforeRoll 
                && (blobSizeLimitBytes == null || blob.Properties.Length < blobSizeLimitBytes);
        }

        private string GetRolledBlobName(string blobName, int rollingSequenceNumber)
        {
            //On first try just return the unchanged blobname
            if (rollingSequenceNumber == 0)
            {
                return blobName;
            }

            //append the sequence number to the filename
            string newFileName = $"{Path.GetFileNameWithoutExtension(blobName)}-{rollingSequenceNumber:D3}{Path.GetExtension(blobName)}";
            return Path.Combine(Path.GetDirectoryName(blobName), newFileName).Replace('\\', '/');
        }

        public async Task<CloudAppendBlob> GetBlobReferenceAsync(CloudBlobClient cloudBlobClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation)
        {      
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(blobContainerName);
            await CreateBlobContainerIfNotExistsAsync(cloudBlobContainer, bypassBlobCreationValidation).ConfigureAwait(false);

            CloudAppendBlob newCloudAppendBlob = null;
            try
            {
                newCloudAppendBlob = cloudBlobContainer.GetAppendBlobReference(blobName);
                newCloudAppendBlob.CreateOrReplaceAsync(AccessCondition.GenerateIfNotExistsCondition(), null, null).GetAwaiter().GetResult();
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict && ex.RequestInformation?.ErrorCode == "BlobAlreadyExists")
            {
                //StorageException (http 409 conflict, error code BlobAlreadyExists) is thrown due to the AccessCondition. The append blob already exists.
                //No problem this is expected
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Failed to create blob: {ex}");
                throw;
            }

            if (newCloudAppendBlob != null)
            {
                //this is the first time the code gets its hands on this blob reference, get the blob properties from azure.
                //used later on to know when to roll over the file if the 50.000 max blocks is getting close.
                await newCloudAppendBlob.FetchAttributesAsync().ConfigureAwait(false);
            }

            return newCloudAppendBlob;
        }

        private async Task CreateBlobContainerIfNotExistsAsync(CloudBlobContainer cloudBlobContainer, bool bypassBlobCreationValidation)
        {
            try
            {
                await cloudBlobContainer.CreateIfNotExistsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Failed to create blob container: {ex}");
                if (!bypassBlobCreationValidation)
                {
                    throw;
                }
            }
        }

        public async Task DeleteArchivedBlobsAsync(CloudBlobClient cloudBlobClient, string blobContainerName, string blobNameFormat, int retainedBlobCountLimit)
        {
            if(retainedBlobCountLimit < 1)
            {
                throw new ArgumentException("Invalid value provided; retained blob count limit must be at least 1 or null.");
            }

            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(blobContainerName);
            BlobContinuationToken blobContinuationToken = null;
            List<IListBlobItem> logBlobs = new List<IListBlobItem>();
            do
            {
                var results = await cloudBlobContainer.ListBlobsSegmentedAsync(null, true, BlobListingDetails.None, null, blobContinuationToken, null, null);
                // Get the value of the continuation token returned by the listing call.
                blobContinuationToken = results.ContinuationToken;
                logBlobs.AddRange(results.Results);                
            } while (blobContinuationToken != null);

            var validLogBlobs = logBlobs.Where(blobItem => {
                return DateTime.TryParseExact(new CloudAppendBlob(blobItem.Uri).Name, 
                    blobNameFormat, 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.AssumeLocal, 
                    out var _date);
            });

            var blobsToDelete = validLogBlobs.OrderByDescending(blob => new CloudAppendBlob(blob.Uri).Name).Skip(retainedBlobCountLimit);
            foreach (IListBlobItem blob in blobsToDelete)
            {
                var blobToDelete = cloudBlobContainer.GetBlobReference(new CloudAppendBlob(blob.Uri).Name);
                await blobToDelete.DeleteIfExistsAsync();
            }
        }
    }
}
