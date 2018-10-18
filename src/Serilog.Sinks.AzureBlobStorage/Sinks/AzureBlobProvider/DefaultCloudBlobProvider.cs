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
using System.Threading;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Serilog.Sinks.AzureBlobStorage.AzureBlobProvider
{
    internal class DefaultCloudBlobProvider : ICloudBlobProvider
    {
        readonly int waitTimeoutMilliseconds = Timeout.Infinite;
        private CloudAppendBlob cloudAppendBlob;

        public CloudAppendBlob GetCloudBlob(CloudStorageAccount storageAccount, string folderName, string fileName, bool bypassBlobCreationValidation)
        {
            if (cloudAppendBlob != null) return cloudAppendBlob;

            var cloudBlobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(folderName);                
            try
            {
                cloudBlobContainer.CreateIfNotExistsAsync().SyncContextSafeWait(waitTimeoutMilliseconds);
                cloudAppendBlob = cloudBlobContainer.GetAppendBlobReference(fileName);                
                if (!cloudAppendBlob.ExistsAsync().Result)
                    cloudAppendBlob.CreateOrReplaceAsync().Wait();                   
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Failed to create blob container: {ex}");
                if (!bypassBlobCreationValidation)
                {
                    throw;
                }                    
            }
            return cloudAppendBlob;

        }
    }
}
