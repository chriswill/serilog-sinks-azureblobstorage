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

using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.AzureBlobStorage;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;

namespace Serilog
{
    /// <summary>
    /// Adds the WriteTo.AzureBlobStorageWithProperties() extension method to <see cref="LoggerConfiguration"/>.
    /// </summary>
    public static class LoggerConfigurationAzureBlobStorageWithPropertiesExtensions
    {
        /// <summary>
        /// A reasonable default for the number of events posted in
        /// each batch.
        /// </summary>
        public const int DefaultBatchPostingLimit = 50;

        /// <summary>
        /// A reasonable default time to wait between checking for event batches.
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Adds a sink that writes log events as records in an Azure Blob Storage blob (default 'logging') using the given storage account.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="storageAccount">The Cloud Storage Account to use to insert the log entries to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="storageFolderName">Folder name that log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink;</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>        
        /// <param name="bypassFolderCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorageWithProperties(
            this LoggerSinkConfiguration loggerConfiguration,
            CloudStorageAccount storageAccount,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            IFormatProvider formatProvider = null,
            string storageFolderName = null,
            string storageFileName = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,            
            bool bypassFolderCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (storageAccount == null) throw new ArgumentNullException(nameof(storageAccount));

            ILogEventSink sink;

            try
            {
                sink = writeInBatches
                    ? (ILogEventSink)
                    new AzureBatchingBlobStorageWithPropertiesSink(storageAccount, formatProvider, batchPostingLimit ?? DefaultBatchPostingLimit, period ?? DefaultPeriod, storageFolderName, storageFileName, bypassFolderCreationValidation, cloudBlobProvider)
                    : new AzureBlobStorageWithPropertiesSink(storageAccount, formatProvider, storageFolderName, storageFileName, bypassFolderCreationValidation, cloudBlobProvider);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorageWithProperties: {ex}");
                sink = new LoggerConfiguration().CreateLogger();
            }

            return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name LogEventEntity) using the given
        /// storage account connection string.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="connectionString">The Cloud Storage Account connection string to use to insert the log entries to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="storageFolderName">Folder name that log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        /// key used for the events so is not enabled by default.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>        
        /// <param name="bypassFolderCreationValidation">Bypass the exception in case the folder creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorageWithProperties(
            this LoggerSinkConfiguration loggerConfiguration,
            string connectionString,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            IFormatProvider formatProvider = null,
            string storageFolderName = null,
            string storageFileName = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,            
            bool bypassFolderCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

            try
            {
                var storageAccount = CloudStorageAccount.Parse(connectionString);
                return AzureBlobStorageWithProperties(loggerConfiguration, storageAccount, restrictedToMinimumLevel, formatProvider, storageFolderName, storageFileName, writeInBatches, period, batchPostingLimit, bypassFolderCreationValidation, cloudBlobProvider);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorageWithProperties: {ex}");

                ILogEventSink sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name LogEventEntity) using the given
        /// storage account name and Shared Access Signature (SAS) URL.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="sharedAccessSignature">The SAS key for the account.</param>
        /// <param name="accountName">The account name.</param>
        /// <param name="blobEndpoint">The (optional) blob endpoint. Only needed for testing.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="storageFolderName">Folder name that log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        /// key used for the events so is not enabled by default.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>        
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        /// /// <exception cref="ArgumentException">A required parameter is empty.</exception>
        public static LoggerConfiguration AzureBlobStorageWithProperties(
            this LoggerSinkConfiguration loggerConfiguration,
            string sharedAccessSignature,
            string accountName,
            Uri blobEndpoint = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            IFormatProvider formatProvider = null,
            string storageFolderName = null,
            string storageFileName = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,            
            ICloudBlobProvider cloudBlobProvider = null)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (string.IsNullOrWhiteSpace(accountName)) throw new ArgumentException(nameof(accountName));
            if (string.IsNullOrWhiteSpace(sharedAccessSignature)) throw new ArgumentException(nameof(sharedAccessSignature));

            try
            {
                var credentials = new StorageCredentials(sharedAccessSignature);
                CloudStorageAccount storageAccount = null;
                if (blobEndpoint == null)
                {
                    storageAccount = new CloudStorageAccount(credentials, accountName, endpointSuffix: null, useHttps: true);
                }
                else
                {
                    storageAccount = new CloudStorageAccount(credentials, null, null, blobEndpoint, null);
                }

                // We set bypassFolderCreationValidation to true explicitly here as the the SAS URL might not have enough permissions to query if the blob exists.
                return AzureBlobStorageWithProperties(loggerConfiguration, storageAccount, restrictedToMinimumLevel, formatProvider, storageFolderName, storageFileName, writeInBatches, period, batchPostingLimit, true, cloudBlobProvider);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorageWithProperties: {ex}");

                ILogEventSink sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
            }
        }
    }
}

