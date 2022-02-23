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

using System.Collections.Generic;
using System.Threading;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;

namespace Serilog.Sinks.AzureBlobStorage
{
    /// <summary>
    /// Writes log events as records to an Azure Blob Storage blob.
    /// </summary>
    public class AzureBlobStorageSink : ILogEventSink
    {
        private readonly int waitTimeoutMilliseconds = Timeout.Infinite;
        private readonly ITextFormatter textFormatter;
        private readonly BlobServiceClient blobServiceClient;
        private readonly string storageContainerName;
        private readonly bool bypassContainerCreationValidation;
        private readonly ICloudBlobProvider cloudBlobProvider;
        private readonly IAppendBlobBlockPreparer appendBlobBlockPreparer;
        private readonly IAppendBlobBlockWriter appendBlobBlockWriter;
        private readonly BlobNameFactory blobNameFactory;
        private readonly long? blobSizeLimitBytes;
        private readonly int? retainedBlobCountLimit;
        private readonly bool useUTCTimezone;

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="blobServiceClient">The Cloud Storage Client to use to insert the log entries to.</param>
        /// <param name="textFormatter"></param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="bypassContainerCreationValidation">Bypass the exception in case the container creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="appendBlobBlockPreparer"></param>
        /// <param name="appendBlobBlockWriter"></param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>  
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUTCTimezone">Use UTC Timezone for logging events</param>
        public AzureBlobStorageSink(
            BlobServiceClient blobServiceClient,
            ITextFormatter textFormatter,
            string storageContainerName = null,
            string storageFileName = null,
            bool bypassContainerCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            IAppendBlobBlockPreparer appendBlobBlockPreparer = null,
            IAppendBlobBlockWriter appendBlobBlockWriter = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUTCTimezone = false)
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
            this.bypassContainerCreationValidation = bypassContainerCreationValidation;
            this.cloudBlobProvider = cloudBlobProvider ?? new DefaultCloudBlobProvider();
            this.appendBlobBlockPreparer = appendBlobBlockPreparer ?? new DefaultAppendBlobBlockPreparer();
            this.appendBlobBlockWriter = appendBlobBlockWriter ?? new DefaultAppendBlobBlockWriter();
            this.blobSizeLimitBytes = blobSizeLimitBytes;
            this.retainedBlobCountLimit = retainedBlobCountLimit;
            this.useUTCTimezone = useUTCTimezone;
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        public void Emit(LogEvent logEvent)
        {
            AppendBlobClient blob = cloudBlobProvider.GetCloudBlobAsync(blobServiceClient, storageContainerName, blobNameFactory.GetBlobName(logEvent.Timestamp, useUTCTimezone), bypassContainerCreationValidation, blobSizeLimitBytes: blobSizeLimitBytes).SyncContextSafeWait(waitTimeoutMilliseconds);

            IEnumerable<string> blocks = appendBlobBlockPreparer.PrepareAppendBlocks(textFormatter, new[] { logEvent });

            appendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blob, blocks).SyncContextSafeWait(waitTimeoutMilliseconds);

            if (retainedBlobCountLimit != null)
            {
                cloudBlobProvider.DeleteArchivedBlobsAsync(blobServiceClient, storageContainerName, blobNameFactory.GetBlobNameFormat(), retainedBlobCountLimit ?? default(int)).SyncContextSafeWait(waitTimeoutMilliseconds);
            }
        }
    }
}

