// Copyright 2014 Serilog Contributors
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

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using System;
using System.Collections.Generic;
using System.IO;
using Serilog.Formatting;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;

namespace Serilog.Sinks.AzureBlobStorage
{
    /// <summary>
    /// Writes log events as records to an Azure Blob Storage blob.
    /// </summary>
    public class AzureBatchingBlobStorageSink : PeriodicBatchingSink
    {
        readonly ITextFormatter textFormatter;        
        readonly CloudStorageAccount storageAccount;
        readonly string storageFolderName;
        readonly string storageFileName;
        readonly bool bypassBlobCreationValidation;
        readonly ICloudBlobProvider cloudBlobProvider;

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="storageAccount">The Cloud Storage Account to use to insert the log entries to.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="batchSizeLimit"></param>
        /// <param name="period"></param>
        /// <param name="storageFolderName">Folder name that log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        public AzureBatchingBlobStorageSink(
            CloudStorageAccount storageAccount,
            IFormatProvider formatProvider,
            ITextFormatter textFormatter,
            int batchSizeLimit,
            TimeSpan period,
            string storageFolderName = null,
            string storageFileName = null,
            ICloudBlobProvider cloudBlobProvider = null)
            : this(storageAccount, textFormatter, batchSizeLimit, period, storageFolderName, storageFileName, cloudBlobProvider: cloudBlobProvider)
        {
        }

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account.
        /// </summary>
        /// <param name="storageAccount">The Cloud Storage Account to use to insert the log entries to.</param>
        /// <param name="textFormatter"></param>
        /// <param name="batchSizeLimit"></param>
        /// <param name="period"></param>
        /// <param name="storageFolderName">Folder name that log entries will be written to. Note: Optional, setting this may impact performance</param>
        /// <param name="storageFileName">File name that log entries will be written to. Note: Optional, setting this may impact performance</param>        
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        public AzureBatchingBlobStorageSink(
            CloudStorageAccount storageAccount,
            ITextFormatter textFormatter,
            int batchSizeLimit,
            TimeSpan period,
            string storageFolderName = null,
            string storageFileName = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null)
            : base(batchSizeLimit, period)
        {

            this.textFormatter = textFormatter;            

            if (string.IsNullOrEmpty(storageFolderName))
            {
                storageFolderName = "logging";
            }

            if (string.IsNullOrEmpty(storageFileName))
            {
                storageFileName = "log.txt";
            }

            this.storageAccount = storageAccount;
            this.storageFolderName = storageFolderName;
            this.storageFileName = storageFileName;
            this.bypassBlobCreationValidation = bypassBlobCreationValidation;
            this.cloudBlobProvider = cloudBlobProvider ?? new DefaultCloudBlobProvider();
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {            
            var blob = cloudBlobProvider.GetCloudBlob(storageAccount, storageFolderName, storageFileName, bypassBlobCreationValidation);

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    foreach (LogEvent logEvent in events)
                    {
                        writer.Write(logEvent.RenderMessage() + Environment.NewLine);
                    }

                    writer.Flush();
                    stream.Position = 0;

                    await blob.AppendBlockAsync(stream);
                }
            }
        }
    }
}
