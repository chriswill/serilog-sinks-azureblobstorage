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
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.AzureBlobStorage
{
    /// <summary>
    /// Writes log events as records to an Azure Blob Storage blob.
    /// </summary>
    public class AzureBatchingBlobStorageSink : IBatchedLogEventSink, ILogEventSink
    {
        private readonly ITextFormatter textFormatter;
        private readonly BlobServiceClient blobServiceClient;
        private readonly string storageContainerName;
        private readonly bool bypassBlobCreationValidation;
        private readonly ICloudBlobProvider cloudBlobProvider;
        private readonly BlobNameFactory blobNameFactory;
        private readonly IAppendBlobBlockPreparer appendBlobBlockPreparer;
        private readonly IAppendBlobBlockWriter appendBlobBlockWriter;
        private readonly string contentType;
        private readonly long? blobSizeLimitBytes;
        private readonly int? retainedBlobCountLimit;
        private readonly bool useUtcTimeZone;

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="blobServiceClient">The Cloud Storage Client to use to insert the log entries to.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="textFormatter">The text formatter to use.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="appendBlobBlockPreparer"></param>
        /// <param name="appendBlobBlockWriter"></param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        public AzureBatchingBlobStorageSink(
            BlobServiceClient blobServiceClient,
            IFormatProvider formatProvider,
            ITextFormatter textFormatter,
            string storageContainerName = null,
            string storageFileName = null,
            ICloudBlobProvider cloudBlobProvider = null,
            IAppendBlobBlockPreparer appendBlobBlockPreparer = null,
            IAppendBlobBlockWriter appendBlobBlockWriter = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUtcTimeZone = false)
            : this(blobServiceClient, textFormatter, storageContainerName, storageFileName, cloudBlobProvider: cloudBlobProvider, appendBlobBlockPreparer: appendBlobBlockPreparer, appendBlobBlockWriter: appendBlobBlockWriter, blobSizeLimitBytes: blobSizeLimitBytes, retainedBlobCountLimit: retainedBlobCountLimit, useUtcTimeZone: useUtcTimeZone)
        {
        }

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="blobServiceClient">The Cloud Storage Client to use to insert the log entries to.</param>
        /// <param name="textFormatter"></param>
        /// <param name="storageContainerName">Container where the log entries will be written to. Note: Optional, setting this may impact performance</param>
        /// <param name="storageFileName">File name that log entries will be written to. Note: Optional, setting this may impact performance</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="appendBlobBlockPreparer"></param>
        /// <param name="appendBlobBlockWriter"></param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        public AzureBatchingBlobStorageSink(
            BlobServiceClient blobServiceClient,
            ITextFormatter textFormatter,
            string storageContainerName = null,
            string storageFileName = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            IAppendBlobBlockPreparer appendBlobBlockPreparer = null,
            IAppendBlobBlockWriter appendBlobBlockWriter = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUtcTimeZone = false)
        {

            this.textFormatter = textFormatter;

            if (string.IsNullOrEmpty(storageContainerName))
            {
                storageContainerName = "logs";
            }

            if (string.IsNullOrEmpty(storageFileName))
            {
                storageFileName = "log.txt";
            }

            this.blobServiceClient = blobServiceClient;
            this.storageContainerName = storageContainerName;
            blobNameFactory = new BlobNameFactory(storageFileName);
            this.bypassBlobCreationValidation = bypassBlobCreationValidation;
            this.cloudBlobProvider = cloudBlobProvider ?? new DefaultCloudBlobProvider();
            this.appendBlobBlockPreparer = appendBlobBlockPreparer ?? new DefaultAppendBlobBlockPreparer();
            this.appendBlobBlockWriter = appendBlobBlockWriter ?? new DefaultAppendBlobBlockWriter();
            this.contentType = contentType;
            this.blobSizeLimitBytes = blobSizeLimitBytes;
            this.retainedBlobCountLimit = retainedBlobCountLimit;
            this.useUtcTimeZone = useUtcTimeZone;
        }

        public Task OnEmptyBatchAsync()
        {
            return Task.CompletedTask;
        }

        public async Task EmitBatchAsync(IEnumerable<LogEvent> logEvents)
        {
            var lastEvent = logEvents.LastOrDefault();
            if (lastEvent == null)
                return;

            Dictionary<AppendBlobClient, List<LogEvent>> logEventsDictionary = new Dictionary<AppendBlobClient, List<LogEvent>>();

            try
            {
                AppendBlobClient blob     = null;
                string           blobName = null;

                foreach (var logEvent in logEvents)
                {
                    var newBlobName = blobNameFactory.GetBlobName(lastEvent.Timestamp, logEvent.Properties, useUtcTimeZone);
                    if (blob == null || blobName != newBlobName)
                    {
                        blobName = newBlobName;
                        blob = await cloudBlobProvider.GetCloudBlobAsync(blobServiceClient, storageContainerName, blobName,
                                                                         bypassBlobCreationValidation, contentType, blobSizeLimitBytes).ConfigureAwait(false);
                    }

                    if (!logEventsDictionary.ContainsKey(blob))
                    {
                        logEventsDictionary.Add(blob, new List<LogEvent> { logEvent });
                    }
                    else
                    {
                        logEventsDictionary[blob].Add(logEvent);
                    }
                }

                foreach (var item in logEventsDictionary)
                {
                    var blocks = appendBlobBlockPreparer.PrepareAppendBlocks(textFormatter, item.Value);

                    await appendBlobBlockWriter.WriteBlocksToAppendBlobAsync(item.Key, blocks).ConfigureAwait(false);
                }

                if (retainedBlobCountLimit != null)
                    await cloudBlobProvider.DeleteArchivedBlobsAsync(blobServiceClient, storageContainerName, blobNameFactory.GetBlobNameFormat(), retainedBlobCountLimit ?? default(int));
            }
            catch (Exception ex)
            { 
                Debugging.SelfLog.WriteLine("Failed to write events to blob storage: {0} {1}", ex.Message, ex.StackTrace);
            }
        }

        public void Emit(LogEvent logEvent)
        {
            Task.Run(() => EmitBatchAsync(new[] { logEvent }));
        }
    }
}
