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
using Azure.Storage;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.AzureBlobStorage;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Serilog.Formatting.Display;
using Azure.Storage.Blobs;
using Azure;
using Azure.Identity;

namespace Serilog
{
    /// <summary>
    /// Adds the WriteTo.AzureBlobStorage() extension method to <see cref="LoggerConfiguration"/>.
    /// </summary>
    public static class LoggerConfigurationAzureBlobStorageExtensions
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


        internal const string DefaultConsoleOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Adds a sink that writes log events as records in an Azure Blob Storage blob (default 'log.txt') using the given storage account.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="blobServiceClient">The Cloud Storage blob service client to use to insert the log entries to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="outputTemplate"> The template to use for writing log entries. The default is '[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}'</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        ///     key used for the events so is not enabled by default.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud Blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUTCTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            BlobServiceClient blobServiceClient,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUTCTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (blobServiceClient == null) throw new ArgumentNullException(nameof(blobServiceClient));

            if (string.IsNullOrEmpty(outputTemplate))
            {
                outputTemplate = DefaultConsoleOutputTemplate;
            }

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                blobServiceClient,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                writeInBatches,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                blobSizeLimitBytes,
                retainedBlobCountLimit, 
                useUTCTimeZone);
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account connection string.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="connectionString">The Cloud Storage Account connection string to use to insert the log entries to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="outputTemplate"> The template to use for writing log entries. The default is '[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}'</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUTCTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            string connectionString,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUTCTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrEmpty(outputTemplate))
            {
                outputTemplate = DefaultConsoleOutputTemplate;
            }

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                connectionString,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                writeInBatches,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                blobSizeLimitBytes,
                retainedBlobCountLimit, 
                useUTCTimeZone);
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account name and Shared Access Signature (SAS) URL.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="sharedAccessSignature">The storage account/blob SAS key.</param>
        /// <param name="accountName">The storage account name.</param>
        /// <param name="blobEndpoint">The (optional) blob endpoint. Only needed for testing.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="outputTemplate"> The template to use for writing log entries. The default is '[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}'</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        ///     key used for the events so is not enabled by default.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUTCTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            string sharedAccessSignature,
            string accountName,
            Uri blobEndpoint = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUTCTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (string.IsNullOrWhiteSpace(accountName)) throw new ArgumentNullException(nameof(accountName));
            if (string.IsNullOrWhiteSpace(sharedAccessSignature))
                throw new ArgumentNullException(nameof(sharedAccessSignature));

            if (string.IsNullOrEmpty(outputTemplate))
            {
                outputTemplate = DefaultConsoleOutputTemplate;
            }

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                sharedAccessSignature,
                accountName,
                blobEndpoint,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                writeInBatches,
                period,
                batchPostingLimit,
                cloudBlobProvider,
                blobSizeLimitBytes,
                retainedBlobCountLimit, 
                useUTCTimeZone);
        }

        /// <summary>
        /// Adds a sink that writes log events as records in an Azure Blob Storage blob (default LogEventEntity) using the given storage account.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatter">Use a Serilog ITextFormatter such as CompactJsonFormatter to store object in Azure blob</param>
        /// <param name="blobServiceClient">The Cloud Storage blob service client to use to insert the log entries to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        ///     key used for the events so is not enabled by default.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUTCTimezone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            BlobServiceClient blobServiceClient,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUTCTimezone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (blobServiceClient == null) throw new ArgumentNullException(nameof(blobServiceClient));
            if (blobSizeLimitBytes != null && blobSizeLimitBytes < 1) throw new ArgumentException("Invalid value provided; file size limit must be at least 1 byte, or null.");
            if (retainedBlobCountLimit != null && retainedBlobCountLimit < 1) throw new ArgumentException("Invalid value provided; retained blob count limit must be at least 1 or null.");

            ILogEventSink sink;
            try
            {
                sink = writeInBatches ?
                    (ILogEventSink)new AzureBatchingBlobStorageSink(blobServiceClient, formatter, batchPostingLimit ?? DefaultBatchPostingLimit, period ?? DefaultPeriod, storageContainerName, storageFileName, bypassBlobCreationValidation, cloudBlobProvider, blobSizeLimitBytes: blobSizeLimitBytes, retainedBlobCountLimit: retainedBlobCountLimit) :
                    new AzureBlobStorageSink(blobServiceClient, formatter, storageContainerName, storageFileName, bypassBlobCreationValidation, cloudBlobProvider, blobSizeLimitBytes: blobSizeLimitBytes, retainedBlobCountLimit: retainedBlobCountLimit, useUTCTimezone: useUTCTimezone);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");
                sink = new LoggerConfiguration().CreateLogger();
            }

            return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account connection string.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatter">Use a Serilog ITextFormatter such as CompactJsonFormatter to store object in data column of Azure blob</param>
        /// <param name="connectionString">The Cloud Storage Account connection string to use to insert the log entries to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        ///     key used for the events so is not enabled by default.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUTCTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            string connectionString,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUTCTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

            try
            {
                var blobServiceClient = new BlobServiceClient(connectionString);

                return AzureBlobStorage(loggerConfiguration, formatter, blobServiceClient, restrictedToMinimumLevel, storageContainerName, storageFileName, writeInBatches, period, batchPostingLimit, bypassBlobCreationValidation, cloudBlobProvider, blobSizeLimitBytes, retainedBlobCountLimit, useUTCTimeZone);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");

                ILogEventSink sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name LogEventEntity) using the given
        /// storage account name and Shared Access Signature (SAS) URL.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatter">Use a Serilog ITextFormatter such as CompactJsonFormatter to store object in data column of Azure blob</param>
        /// <param name="sharedAccessSignature">The storage account/blob SAS key.</param>
        /// <param name="accountName">The storage account name.</param>
        /// <param name="blobEndpoint">The (optional) blob endpoint. Only needed for testing.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        ///     key used for the events so is not enabled by default.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUTCTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            string sharedAccessSignature,
            string accountName,
            Uri blobEndpoint = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUTCTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (string.IsNullOrWhiteSpace(accountName)) throw new ArgumentNullException(nameof(accountName));
            if (string.IsNullOrWhiteSpace(sharedAccessSignature)) throw new ArgumentNullException(nameof(sharedAccessSignature));

            try
            {
                //  TODO-VPL:  very likely messed it up here, not sure what that special case was supposed to do
                //  with a no-endpoint client
                if (blobEndpoint == null)
                {
                    throw new NotSupportedException($"'{nameof(blobEndpoint)}' must be provided");
                }
                else
                {
                    var credentials = new AzureSasCredential(sharedAccessSignature);
                    var blobServiceClient = new BlobServiceClient(blobEndpoint, credentials);

                    // We set bypassBlobCreationValidation to true explicitly here as the the SAS URL might not have enough permissions to query if the blob exists.
                    return AzureBlobStorage(loggerConfiguration, formatter, blobServiceClient, restrictedToMinimumLevel, storageContainerName, storageFileName, writeInBatches, period, batchPostingLimit, true, cloudBlobProvider, blobSizeLimitBytes, retainedBlobCountLimit, useUTCTimeZone);
                }
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");

                ILogEventSink sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') with authentictaion using Azure Identity
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatter">Use a Serilog ITextFormatter such as CompactJsonFormatter to store object in data column of Azure blob</param>
        /// <param name="storageAccountUri">The Cloud Storage Account Uri to use to authenticate using Azure Identity</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink; this alters the partition
        ///     key used for the events so is not enabled by default.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="managedIdentityClientId">Specifies the client id of the Azure ManagedIdentity in the case of user assigned identity.</param>
        /// <param name="useUTCTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            Uri storageAccountUri,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            string managedIdentityClientId = null, 
            bool useUTCTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));

            try
            {
                DefaultAzureCredential defaultAzureCredential;
                if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
                {
                    defaultAzureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                        {ManagedIdentityClientId = managedIdentityClientId });
                }
                else
                {
                    defaultAzureCredential = new DefaultAzureCredential();
                }
                var blobServiceClient = new BlobServiceClient(storageAccountUri, defaultAzureCredential);

                return AzureBlobStorage(loggerConfiguration, formatter, blobServiceClient, restrictedToMinimumLevel, storageContainerName, storageFileName, writeInBatches, period, batchPostingLimit, bypassBlobCreationValidation, cloudBlobProvider, blobSizeLimitBytes, retainedBlobCountLimit, useUTCTimeZone);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");

                ILogEventSink sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account connection string.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="storageAccountUri">The Cloud Storage Account Uri to use to authenticate using Azure Identity</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="outputTemplate"> The template to use for writing log entries. The default is '[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}'</param>
        /// <param name="writeInBatches">Use a periodic batching sink, as opposed to a synchronous one-at-a-time sink.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="managedIdentityClientId">Specifies the client id of the Azure ManagedIdentity in the case of user assigned identity.</param>
        /// <param name="useUTCTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            Uri storageAccountUri,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = null,
            bool writeInBatches = false,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            string managedIdentityClientId = null, 
            bool useUTCTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));

            if (string.IsNullOrEmpty(outputTemplate))
            {
                outputTemplate = DefaultConsoleOutputTemplate;
            }

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                storageAccountUri,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                writeInBatches,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                blobSizeLimitBytes,
                retainedBlobCountLimit,
                managedIdentityClientId, 
                useUTCTimeZone);
        }
    }
}
