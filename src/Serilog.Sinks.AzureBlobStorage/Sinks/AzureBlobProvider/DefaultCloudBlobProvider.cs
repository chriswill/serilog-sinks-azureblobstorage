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
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Serilog.Sinks.AzureBlobStorage.AzureBlobProvider
{
    internal class DefaultCloudBlobProvider : ICloudBlobProvider
    {
        private CloudAppendBlob currentCloudAppendBlob;
        private string currentBlobName = string.Empty;
        private int currentBlobRollSequence = 0;

        private static readonly int MaxBlocksOnBlobBeforeRoll = 49500; //small margin to the practical max of 50k, in case of many multiple writers to the same blob

        public async Task<CloudAppendBlob> GetCloudBlobAsync(CloudStorageAccount storageAccount, string blobContainerName, string blobName, bool bypassBlobCreationValidation)
        {
            if (currentCloudAppendBlob != null && currentBlobName.Equals(blobName, StringComparison.OrdinalIgnoreCase) && currentCloudAppendBlob.Properties.AppendBlobCommittedBlockCount < MaxBlocksOnBlobBeforeRoll)
            {
                //if the correct cloud append blob is prepared and below the max block count then return that
                return currentCloudAppendBlob;
            }

            if (currentCloudAppendBlob != null && currentBlobName.Equals(blobName, StringComparison.OrdinalIgnoreCase))
            {
                //same blob name, but the max blocks have been reached, roll the sequence one up and get a new cloud blob reference
                currentBlobRollSequence++;
                await GetCloudAppendBlobAsync(storageAccount, blobContainerName, blobName, bypassBlobCreationValidation);
            }
            else
            {
                //first time to get a cloudblob or the blobname has changed
                currentBlobRollSequence = 0;
                await GetCloudAppendBlobAsync(storageAccount, blobContainerName, blobName, bypassBlobCreationValidation);
            }

            return currentCloudAppendBlob;
        }

        private async Task GetCloudAppendBlobAsync(CloudStorageAccount storageAccount, string blobContainerName, string blobName, bool bypassBlobCreationValidation)
        {
            //try to get a reference to a cloudappendblob which is below the max blocks threshold.
            for (int i = currentBlobRollSequence; i < 999; i++)
            {
                string rolledBlobName = GetRolledBlobName(blobName, i);
                CloudAppendBlob newCloudAppendBlob = await GetBlobReferenceAsync(storageAccount, blobContainerName, rolledBlobName, bypassBlobCreationValidation);
                if (newCloudAppendBlob.Properties.AppendBlobCommittedBlockCount < MaxBlocksOnBlobBeforeRoll)
                {
                    currentCloudAppendBlob = newCloudAppendBlob;
                    currentBlobName = blobName;
                    currentBlobRollSequence = i;
                    break;
                }
            }
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

        private async Task<CloudAppendBlob> GetBlobReferenceAsync(CloudStorageAccount storageAccount, string blobContainerName, string blobName, bool bypassBlobCreationValidation)
        {
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
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
    }
}
