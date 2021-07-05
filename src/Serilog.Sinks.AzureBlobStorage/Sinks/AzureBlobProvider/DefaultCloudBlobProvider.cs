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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Serilog.Sinks.AzureBlobStorage.AzureBlobProvider
{
    internal class DefaultCloudBlobProvider : ICloudBlobProvider
    {
        private AppendBlobClient currentAppendBlobClient;
        private string currentBlobName = string.Empty;
        private int currentBlobRollSequence = 0;

        private static readonly int MaxBlocksOnBlobBeforeRoll = 49500; //small margin to the practical max of 50k, in case of many multiple writers to the same blob

        public async Task<AppendBlobClient> GetCloudBlobAsync(BlobServiceClient blobServiceClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation, long? blobSizeLimitBytes = null)
        {
            // Check if the current known blob is the targeted blob
            if (currentAppendBlobClient != null && currentBlobName.Equals(blobName, StringComparison.OrdinalIgnoreCase))
            {
                // Before performing validate first fetch attributes for current file size
                var propertiesResponse = await currentAppendBlobClient.GetPropertiesAsync().ConfigureAwait(false);
                var properties = propertiesResponse.Value;

                // Check if the current blob is within the block count and file size limits
                if (ValidateBlobProperties(properties, blobSizeLimitBytes))
                {
                    return currentAppendBlobClient;
                }
                else
                {
                    // The blob is correct but needs to be rolled over
                    currentBlobRollSequence++;
                    await GetAppendBlobClientAsync(blobServiceClient, blobContainerName, blobName, bypassBlobCreationValidation);
                }
            }
            else
            {
                //first time to get a cloudblob or the blobname has changed
                currentBlobRollSequence = 0;
                await GetAppendBlobClientAsync(blobServiceClient, blobContainerName, blobName, bypassBlobCreationValidation, blobSizeLimitBytes);
            }

            return currentAppendBlobClient;
        }

        private async Task GetAppendBlobClientAsync(BlobServiceClient blobServiceClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation, long? blobSizeLimitBytes = null)
        {
            //try to get a reference to a AppendBlobClient which is below the max blocks threshold.
            for (int i = currentBlobRollSequence; i < 999; i++)
            {
                string rolledBlobName = GetRolledBlobName(blobName, i);
                AppendBlobClient newAppendBlobClient = await GetBlobReferenceAsync(blobServiceClient, blobContainerName, rolledBlobName, bypassBlobCreationValidation);
                var blobPropertiesResponse = await newAppendBlobClient.GetPropertiesAsync();
                var blobProperties = blobPropertiesResponse.Value;
                
                if (ValidateBlobProperties(blobProperties, blobSizeLimitBytes))
                {
                    currentAppendBlobClient = newAppendBlobClient;
                    currentBlobName = blobName;
                    currentBlobRollSequence = i;
                    break;
                }
            }
        }

        private bool ValidateBlobProperties(BlobProperties properties, long? blobSizeLimitBytes = null)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            return properties.BlobCommittedBlockCount < MaxBlocksOnBlobBeforeRoll
                && (blobSizeLimitBytes == null || properties.ContentLength < blobSizeLimitBytes);
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

        public async Task<AppendBlobClient> GetBlobReferenceAsync(BlobServiceClient blobServiceClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation)
        {
            var blobContainer = blobServiceClient.GetBlobContainerClient(blobContainerName);

            await CreateBlobContainerIfNotExistsAsync(blobContainer, bypassBlobCreationValidation).ConfigureAwait(false);

            AppendBlobClient newAppendBlobClient = null;
            try
            {
                newAppendBlobClient = blobContainer.GetAppendBlobClient(blobName);

                //  TODO-VPL:  CreateOrReplaceAsync does not exist in the new SDK
                //  TODO-VPL:  AccessCondition is nowhere to be seen...  here is the original line:
                //newAppendBlobClient.CreateOrReplaceAsync(AccessCondition.GenerateIfNotExistsCondition(), null, null).GetAwaiter().GetResult();
                await newAppendBlobClient.CreateIfNotExistsAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict && ex.ErrorCode == "BlobAlreadyExists")
            {
                //StorageException (http 409 conflict, error code BlobAlreadyExists) is thrown due to the AccessCondition. The append blob already exists.
                //No problem this is expected
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Failed to create blob: {ex}");
                throw;
            }

            //  TODO-VPL:  This is done differently in the new SDK ; we need to do a get properties and they return the properties, i.e. done elsewhere
            //if (newAppendBlobClient != null)
            //{
            //    //this is the first time the code gets its hands on this blob reference, get the blob properties from azure.
            //    //used later on to know when to roll over the file if the 50.000 max blocks is getting close.
            //    await newAppendBlobClient.FetchAttributesAsync().ConfigureAwait(false);
            //}

            return newAppendBlobClient;
        }

        private async Task CreateBlobContainerIfNotExistsAsync(BlobContainerClient blobContainerClient, bool bypassBlobCreationValidation)
        {
            try
            {
                await blobContainerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
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

        public async Task DeleteArchivedBlobsAsync(BlobServiceClient blobServiceClient, string blobContainerName, string blobNameFormat, int retainedBlobCountLimit)
        {
            if (retainedBlobCountLimit < 1)
            {
                throw new ArgumentException("Invalid value provided; retained blob count limit must be at least 1 or null.");
            }

            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(blobContainerName);
            List<BlobItem> logBlobs = new List<BlobItem>();

            AsyncPageable<BlobItem> blobItems = blobContainer.GetBlobsAsync();
            
            IAsyncEnumerator<BlobItem> enumerator = blobItems.GetAsyncEnumerator();
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    logBlobs.Add(enumerator.Current);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            IEnumerable<BlobItem> validLogBlobs = logBlobs.Where(blobItem => DateTime.TryParseExact(
                RemoveRolledBlobNameSerialNum(blobItem.Name),
                blobNameFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var _date));

            IEnumerable<BlobItem> blobsToDelete = validLogBlobs
                .OrderByDescending(blobItem => blobItem.Name)
                .Skip(retainedBlobCountLimit);

            foreach (var blobItem in blobsToDelete)
            {
                AppendBlobClient blobToDelete = blobContainer.GetAppendBlobClient(blobItem.Name);

                await blobToDelete.DeleteIfExistsAsync();
            }
        }

        private string RemoveRolledBlobNameSerialNum(string blobName)
        {
            string blobNameWoExtension = Path.ChangeExtension(blobName, null);
            blobNameWoExtension = Regex.Replace(blobNameWoExtension, "-[0-9]{3}$", String.Empty);
            return blobNameWoExtension + Path.GetExtension(blobName);
        }
    }
}
