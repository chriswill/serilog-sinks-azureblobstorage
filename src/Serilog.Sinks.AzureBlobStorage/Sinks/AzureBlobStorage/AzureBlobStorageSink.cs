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

using System;
using System.Collections.Generic;
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
        // Upper bound on the number of blob names remembered for deciding when old blobs need to be cleaned up.
        private const int MaxRememberedBlobNames = 1024;

        private readonly BlobServiceClient blobServiceClient;
        private readonly AzureBlobStorageSinkOptions options;
        private readonly BlobNameFactory blobNameFactory;

        private readonly HashSet<string> writtenBlobNames = new HashSet<string>(StringComparer.Ordinal);
        private bool retentionPending;

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
                        await WriteAsync(currentBlobName, currentLogBatch).ConfigureAwait(false);
                    }
                    currentBlobName = blobName;
                    currentLogBatch = new List<LogEvent>{logEvent};
                }
            }

            //Send any remaining items in the batch
            if (currentLogBatch.Count > 0)
            {
                await WriteAsync(currentBlobName, currentLogBatch).ConfigureAwait(false);
            }

            //Delete old blobs if the limit is set
            if (options.RetainedBlobCountLimit != null && retentionPending)
            {
                await DeleteArchivedBlobsAsync(options.RetainedBlobCountLimit.Value).ConfigureAwait(false);
            }
        }

        private async Task WriteAsync(string blobName, List<LogEvent> logEvents)
        {
            AppendBlobClient blob = await options.CloudBlobProvider.GetCloudBlobAsync(blobServiceClient, options.StorageContainerName, blobName, options.BypassContainerCreationValidation, options.ContentType, options.BlobSizeLimitBytes).ConfigureAwait(false);
            IEnumerable<string> blocks = options.AppendBlobBlockPreparer.PrepareAppendBlocks(options.Formatter, logEvents);
            await options.AppendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blob, blocks).ConfigureAwait(false);

            if (options.RetainedBlobCountLimit != null)
            {
                TrackWrittenBlob(blob?.Name ?? blobName);
            }
        }

        /// <summary>
        /// Old blobs only need to be cleaned up when this sink starts writing to a blob that it
        /// has not written to before, which includes blobs that were rolled over.
        /// </summary>
        private void TrackWrittenBlob(string blobName)
        {
            if (writtenBlobNames.Contains(blobName))
            {
                return;
            }

            if (writtenBlobNames.Count >= MaxRememberedBlobNames)
            {
                writtenBlobNames.Clear();
            }

            writtenBlobNames.Add(blobName);
            retentionPending = true;
        }

        private async Task DeleteArchivedBlobsAsync(int retainedBlobCountLimit)
        {
            try
            {
                await options.CloudBlobProvider.DeleteArchivedBlobsAsync(blobServiceClient, options.StorageContainerName, options.StorageFileName, retainedBlobCountLimit).ConfigureAwait(false);
                retentionPending = false;
            }
            catch (Exception ex)
            {
                // The events of this batch have been written already. Failing the batch here would make
                // Serilog write them again, so the clean-up is attempted again with the next batch instead.
                Debugging.SelfLog.WriteLine("Failed to delete archived blobs: {0}", ex);
            }
        }

        public Task OnEmptyBatchAsync() => Task.CompletedTask;
    }
}
