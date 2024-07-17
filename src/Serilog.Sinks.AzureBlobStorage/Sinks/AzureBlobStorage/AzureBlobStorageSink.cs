// Copyright 2024 CloudScope, LLC
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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.AzureBlobStorage
{
    /// <summary>
    /// Writes log events as records to an Azure Blob Storage blob.
    /// </summary>
    public class AzureBlobStorageSink : IBatchedLogEventSink
    {
        private readonly BlobServiceClient blobServiceClient;
        private readonly AzureBlobStorageSinkOptions options;
        private readonly BlobNameFactory blobNameFactory;

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="blobServiceClient">The Cloud Storage Client to use to insert the log entries to.</param>
        /// <param name="options">The options to create the blob storage sink</param>
        public AzureBlobStorageSink(BlobServiceClient blobServiceClient, AzureBlobStorageSinkOptions options)
        {
            this.blobServiceClient = blobServiceClient;
            this.options = options;
            blobNameFactory = new BlobNameFactory(options.StorageFileName);
        }

        public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
        {
            string currentBlobName = string.Empty;
            List<LogEvent> currentLogBatch = new List<LogEvent>();

            //iterating through the events in the batch, create a new AppendBlobClient each time a new name is required
            foreach (LogEvent logEvent in batch)
            {
                string blobName = blobNameFactory.GetBlobName(logEvent.Timestamp, logEvent.Level, logEvent.Properties, options.UseUtcTimezone);
                if (blobName.Equals(currentBlobName))
                {
                    currentLogBatch.Add(logEvent);
                }
                else
                {
                    if (currentBlobName != string.Empty)
                    {
                        AppendBlobClient blob = await options.CloudBlobProvider.GetCloudBlobAsync(blobServiceClient, options.StorageContainerName, currentBlobName, options.BypassContainerCreationValidation, options.ContentType, options.BlobSizeLimitBytes);
                        IEnumerable<string> blocks = options.AppendBlobBlockPreparer.PrepareAppendBlocks(options.Formatter, currentLogBatch);
                        await options.AppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blob, blocks);
                    }
                    currentBlobName = blobName;
                    currentLogBatch = new List<LogEvent>{logEvent};
                }
            }

            //Send any remaining items in the batch
            if (currentLogBatch.Any())
            {
                AppendBlobClient blob = await options.CloudBlobProvider.GetCloudBlobAsync(blobServiceClient, options.StorageContainerName, currentBlobName, options.BypassContainerCreationValidation, options.ContentType, options.BlobSizeLimitBytes);
                IEnumerable<string> blocks = options.AppendBlobBlockPreparer.PrepareAppendBlocks(options.Formatter, currentLogBatch);
                await options.AppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blob, blocks);
            }

            //Delete old blobs if the limit is set
            if (options.RetainedBlobCountLimit != null)
            {
                await options.CloudBlobProvider.DeleteArchivedBlobsAsync(blobServiceClient, options.StorageContainerName, options.StorageFileName, options.RetainedBlobCountLimit ?? default(int));
            }
        }

        public Task OnEmptyBatchAsync() => Task.CompletedTask;
    }
}

