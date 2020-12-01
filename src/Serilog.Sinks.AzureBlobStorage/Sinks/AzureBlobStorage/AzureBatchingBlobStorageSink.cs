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
using Microsoft.Azure.Storage.Blob;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.AzureBlobStorage
{
    /// <summary>
    /// Writes log events as records to an Azure Blob Storage blob.
    /// </summary>
    public class AzureBatchingBlobStorageSink : PeriodicBatchingSink
    {
        private readonly ITextFormatter textFormatter;
        private readonly CloudBlobClient cloudBlobClient;
        private readonly string storageContainerName;
        private readonly bool bypassBlobCreationValidation;
        private readonly ICloudBlobProvider cloudBlobProvider;
        private readonly BlobNameFactory blobNameFactory;
        private readonly IAppendBlobBlockPreparer appendBlobBlockPreparer;
        private readonly IAppendBlobBlockWriter appendBlobBlockWriter;
        private readonly long? blobSizeLimitBytes;
        private readonly int? retainedBlobCountLimit;

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="cloudBlobClient">The Cloud Storage Client to use to insert the log entries to.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="textFormatter">The text formatter to use.</param>
        /// <param name="batchSizeLimit"></param>
        /// <param name="period"></param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="appendBlobBlockPreparer"></param>
        /// <param name="appendBlobBlockWriter"></param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        public AzureBatchingBlobStorageSink(
            CloudBlobClient cloudBlobClient,
            IFormatProvider formatProvider,
            ITextFormatter textFormatter,
            int batchSizeLimit,
            TimeSpan period,
            string storageContainerName = null,
            string storageFileName = null,
            ICloudBlobProvider cloudBlobProvider = null,
            IAppendBlobBlockPreparer appendBlobBlockPreparer = null,
            IAppendBlobBlockWriter appendBlobBlockWriter = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null)
            : this(cloudBlobClient, textFormatter, batchSizeLimit, period, storageContainerName, storageFileName, cloudBlobProvider: cloudBlobProvider, appendBlobBlockPreparer: appendBlobBlockPreparer, appendBlobBlockWriter: appendBlobBlockWriter, blobSizeLimitBytes: blobSizeLimitBytes, retainedBlobCountLimit: retainedBlobCountLimit)
        {
        }

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="cloudBlobClient">The Cloud Storage Client to use to insert the log entries to.</param>
        /// <param name="textFormatter"></param>
        /// <param name="batchSizeLimit"></param>
        /// <param name="period"></param>
        /// <param name="storageContainerName">Container where the log entries will be written to. Note: Optional, setting this may impact performance</param>
        /// <param name="storageFileName">File name that log entries will be written to. Note: Optional, setting this may impact performance</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="appendBlobBlockPreparer"></param>
        /// <param name="appendBlobBlockWriter"></param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        public AzureBatchingBlobStorageSink(
            CloudBlobClient cloudBlobClient,
            ITextFormatter textFormatter,
            int batchSizeLimit,
            TimeSpan period,
            string storageContainerName = null,
            string storageFileName = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            IAppendBlobBlockPreparer appendBlobBlockPreparer = null,
            IAppendBlobBlockWriter appendBlobBlockWriter = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null)
            : base(batchSizeLimit, period)
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

            this.cloudBlobClient = cloudBlobClient;
            this.storageContainerName = storageContainerName;
            this.blobNameFactory = new BlobNameFactory(storageFileName);
            this.bypassBlobCreationValidation = bypassBlobCreationValidation;
            this.cloudBlobProvider = cloudBlobProvider ?? new DefaultCloudBlobProvider();
            this.appendBlobBlockPreparer = appendBlobBlockPreparer ?? new DefaultAppendBlobBlockPreparer();
            this.appendBlobBlockWriter = appendBlobBlockWriter ?? new DefaultAppendBlobBlockWriter();
            this.blobSizeLimitBytes = blobSizeLimitBytes;
            this.retainedBlobCountLimit = retainedBlobCountLimit;
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            var lastEvent = events.LastOrDefault();
            if (lastEvent == null)
                return;

            if(retainedBlobCountLimit != null)
                await cloudBlobProvider.DeleteArchivedBlobsAsync(cloudBlobClient, storageContainerName, blobNameFactory.GetBlobNameFormat(), retainedBlobCountLimit ?? default(int));

            var blob = await cloudBlobProvider.GetCloudBlobAsync(cloudBlobClient, storageContainerName, blobNameFactory.GetBlobName(lastEvent.Timestamp), bypassBlobCreationValidation, blobSizeLimitBytes).ConfigureAwait(false);

            var blocks = appendBlobBlockPreparer.PrepareAppendBlocks(textFormatter, events);

            await appendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blob, blocks).ConfigureAwait(false);
        }
    }
}
