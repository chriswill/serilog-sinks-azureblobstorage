﻿// Copyright 2018 CloudScope, LLC
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

using System.Threading;
using Microsoft.WindowsAzure.Storage;
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
        private readonly CloudStorageAccount storageAccount;
        private readonly string storageFolderName;
        private readonly bool bypassFolderCreationValidation;
        private readonly ICloudBlobProvider cloudBlobProvider;
        private readonly IAppendBlobBlockPreparer appendBlobBlockPreparer;
        private readonly IAppendBlobBlockWriter appendBlobBlockWriter;
        private readonly BlobNameFactory blobNameFactory;

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="storageAccount">The Cloud Storage Account to use to insert the log entries to.</param>
        /// <param name="textFormatter"></param>
        /// <param name="storageFolderName">Folder name that log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>        
        /// <param name="bypassFolderCreationValidation">Bypass the exception in case the folder creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="appendBlobBlockPreparer"></param>
        /// <param name="appendBlobBlockWriter"></param>
        public AzureBlobStorageSink(
            CloudStorageAccount storageAccount,
            ITextFormatter textFormatter,
            string storageFolderName = null,
            string storageFileName = null,
            bool bypassFolderCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            IAppendBlobBlockPreparer appendBlobBlockPreparer = null,
            IAppendBlobBlockWriter appendBlobBlockWriter = null)
        {
            this.textFormatter = textFormatter;

            if (string.IsNullOrEmpty(storageFolderName))
            {
                storageFolderName = "logs";
            }

            if (string.IsNullOrEmpty(storageFileName))
            {
                storageFileName = "log.txt";
            }

            this.storageAccount = storageAccount;
            this.storageFolderName = storageFolderName;
            blobNameFactory = new BlobNameFactory(storageFileName);
            this.bypassFolderCreationValidation = bypassFolderCreationValidation;
            this.cloudBlobProvider = cloudBlobProvider ?? new DefaultCloudBlobProvider();
            this.appendBlobBlockPreparer = appendBlobBlockPreparer ?? new DefaultAppendBlobBlockPreparer();
            this.appendBlobBlockWriter = appendBlobBlockWriter ?? new DefaultAppendBlobBlockWriter();
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        public void Emit(LogEvent logEvent)
        {
            var blob = cloudBlobProvider.GetCloudBlobAsync(storageAccount, storageFolderName, blobNameFactory.GetBlobName(logEvent.Timestamp), bypassFolderCreationValidation).SyncContextSafeWait(waitTimeoutMilliseconds);

            var blocks = appendBlobBlockPreparer.PrepareAppendBlocks(textFormatter, new[] { logEvent });

            appendBlobBlockWriter.WriteBlocksToAppendBlobAsync(blob, blocks).SyncContextSafeWait(waitTimeoutMilliseconds);
        }
    }
}

