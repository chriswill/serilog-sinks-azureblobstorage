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
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.AzureBlobStorage;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Serilog.Formatting.Display;
using Azure.Storage.Blobs;
using Azure;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Serilog.Core;

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
        public const int DefaultBatchSizeLimit = 1000;

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
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud Blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            BlobServiceClient blobServiceClient,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = DefaultConsoleOutputTemplate,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (blobServiceClient == null) throw new ArgumentNullException(nameof(blobServiceClient));

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                blobServiceClient,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                contentType,
                blobSizeLimitBytes,
                retainedBlobCountLimit,
                useUtcTimeZone);
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
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            string connectionString,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = DefaultConsoleOutputTemplate,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                connectionString,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                contentType,
                blobSizeLimitBytes,
                retainedBlobCountLimit,
                useUtcTimeZone);
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account connection string.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="connectionStringName">The name of the connection string to use to connect to Azure Storage.</param>
        /// <param name="configuration">The injected or provided IConfiguration instance</param>
        /// <param name="outputTemplate"> The template to use for writing log entries. The default is '[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}'</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(
            this LoggerSinkConfiguration loggerConfiguration,
            string connectionStringName,
            IConfiguration configuration,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = DefaultConsoleOutputTemplate,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (string.IsNullOrEmpty(connectionStringName)) throw new ArgumentNullException(nameof(connectionStringName));

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                connectionStringName,
                configuration,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                contentType,
                blobSizeLimitBytes,
                retainedBlobCountLimit,
                useUtcTimeZone);
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account name and Shared Access Signature (SAS) URL.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="sharedAccessSignature">The storage account/blob SAS key.</param>
        /// <param name="accountUrl">The blob endpoint, in string format. Either this or blobEndpoint is required. Recommended for use with appsettings configuration. Example: https://myaccount.blob.core.windows.net</param>
        /// <param name="blobEndpoint">The blob endpoint, in Uri format.  Either this or accountUrl is required.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="outputTemplate"> The template to use for writing log entries. The default is '[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}'</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            string sharedAccessSignature,
            string accountUrl = null,
            Uri blobEndpoint = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = DefaultConsoleOutputTemplate,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (string.IsNullOrWhiteSpace(accountUrl) && blobEndpoint == null) throw new ArgumentNullException(nameof(accountUrl));
            if (string.IsNullOrWhiteSpace(sharedAccessSignature))
                throw new ArgumentNullException(nameof(sharedAccessSignature));

            return AzureBlobStorage(
                loggerConfiguration,
                new MessageTemplateTextFormatter(outputTemplate, formatProvider),
                sharedAccessSignature,
                accountUrl,
                blobEndpoint,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                contentType,
                blobSizeLimitBytes,
                retainedBlobCountLimit,
                useUtcTimeZone);
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
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            string connectionString,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

            AzureBlobStorageSinkOptions options = new AzureBlobStorageSinkOptions();
            options.Formatter = formatter;
            if (!string.IsNullOrEmpty(storageContainerName)) options.StorageContainerName = storageContainerName;
            if (!string.IsNullOrEmpty(storageFileName)) options.StorageFileName = storageFileName;
            options.BypassContainerCreationValidation = bypassBlobCreationValidation;
            if (cloudBlobProvider != null) options.CloudBlobProvider = cloudBlobProvider;
            if (!string.IsNullOrEmpty(contentType)) options.ContentType = contentType;
            options.BlobSizeLimitBytes = blobSizeLimitBytes;
            options.RetainedBlobCountLimit = retainedBlobCountLimit;
            options.UseUtcTimezone = useUtcTimeZone;
            
            try
            {
                AzureBlobStorageSink blobStorageSink = new AzureBlobStorageSink(blobServiceClient, options);

                BatchingOptions batchingOptions = new BatchingOptions
                {
                    BatchSizeLimit = batchPostingLimit ?? DefaultBatchSizeLimit,
                    EagerlyEmitFirstEvent = true,
                    BufferingTimeLimit = period ?? DefaultPeriod,
                };

                return loggerConfiguration.Sink(blobStorageSink, batchingOptions, restrictedToMinimumLevel);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");
                var sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account connection string.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatter">Use a Serilog ITextFormatter such as CompactJsonFormatter to store object in data column of Azure blob</param>
        /// <param name="connectionStringName">The name of the connection string to use to connect to Azure Storage.</param>
        /// <param name="configuration">The injected or provided IConfiguration instance</param>/// 
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            string connectionStringName,
            IConfiguration configuration,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (string.IsNullOrEmpty(connectionStringName)) throw new ArgumentNullException(nameof(connectionStringName));

            if (configuration == null) throw new ArgumentNullException(nameof(configuration), "IConfiguration was null; must inject or provide it");
            string connectionString = configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException($"Connection string '{connectionStringName}' could not be found");
            }

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            
            AzureBlobStorageSinkOptions options = new AzureBlobStorageSinkOptions();
            options.Formatter = formatter;
            if (!string.IsNullOrEmpty(storageContainerName)) options.StorageContainerName = storageContainerName;
            if (!string.IsNullOrEmpty(storageFileName)) options.StorageFileName = storageFileName;
            options.BypassContainerCreationValidation = bypassBlobCreationValidation;
            if (cloudBlobProvider != null) options.CloudBlobProvider = cloudBlobProvider;
            if (!string.IsNullOrEmpty(contentType)) options.ContentType = contentType;
            options.BlobSizeLimitBytes = blobSizeLimitBytes;
            options.RetainedBlobCountLimit = retainedBlobCountLimit;
            options.UseUtcTimezone = useUtcTimeZone;

            try
            {
                AzureBlobStorageSink blobStorageSink = new AzureBlobStorageSink(blobServiceClient, options);

                BatchingOptions batchingOptions = new BatchingOptions
                {
                    BatchSizeLimit = batchPostingLimit ?? DefaultBatchSizeLimit,
                    EagerlyEmitFirstEvent = true,
                    BufferingTimeLimit = period ?? DefaultPeriod,
                };

                return loggerConfiguration.Sink(blobStorageSink, batchingOptions, restrictedToMinimumLevel);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");
                var sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name LogEventEntity) using the given
        /// storage account name and Shared Access Signature (SAS) URL.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatter">Use a Serilog ITextFormatter such as CompactJsonFormatter to store object in data column of Azure blob</param>
        /// <param name="sharedAccessSignature">The storage account/blob SAS key.</param>
        /// <param name="accountUrl">The blob endpoint, in string format. Either this or blobEndpoint is required. Recommended for use with appsettings configuration. Example: https://myaccount.blob.core.windows.net</param>
        /// <param name="blobEndpoint">The blob endpoint, in Uri format.  Either this or accountUrl is required.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation"></param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            string sharedAccessSignature,
            string accountUrl = null,
            Uri blobEndpoint = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null, 
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (string.IsNullOrWhiteSpace(accountUrl) && blobEndpoint == null) throw new ArgumentNullException(nameof(accountUrl));
            if (string.IsNullOrWhiteSpace(sharedAccessSignature)) throw new ArgumentNullException(nameof(sharedAccessSignature));

            AzureSasCredential credentials = new AzureSasCredential(sharedAccessSignature); 
            BlobServiceClient blobServiceClient = new BlobServiceClient(blobEndpoint ?? new Uri(accountUrl), credentials);

            AzureBlobStorageSinkOptions options = new AzureBlobStorageSinkOptions();
            options.Formatter = formatter;
            if (!string.IsNullOrEmpty(storageContainerName)) options.StorageContainerName = storageContainerName;
            if (!string.IsNullOrEmpty(storageFileName)) options.StorageFileName = storageFileName;
            options.BypassContainerCreationValidation = bypassBlobCreationValidation;
            if (cloudBlobProvider != null) options.CloudBlobProvider = cloudBlobProvider;
            if (!string.IsNullOrEmpty(contentType)) options.ContentType = contentType;
            options.BlobSizeLimitBytes = blobSizeLimitBytes;
            options.RetainedBlobCountLimit = retainedBlobCountLimit;
            options.UseUtcTimezone = useUtcTimeZone;

            try
            {
                AzureBlobStorageSink blobStorageSink = new AzureBlobStorageSink(blobServiceClient, options);

                BatchingOptions batchingOptions = new BatchingOptions
                {
                    BatchSizeLimit = batchPostingLimit ?? DefaultBatchSizeLimit,
                    EagerlyEmitFirstEvent = true,
                    BufferingTimeLimit = period ?? DefaultPeriod,
                };

                return loggerConfiguration.Sink(blobStorageSink, batchingOptions, restrictedToMinimumLevel);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");
                var sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') with authentictaion using Azure Identity
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="formatter">Use a Serilog ITextFormatter such as CompactJsonFormatter to store object in data column of Azure blob</param>
        /// <param name="managedIdentityClientId">Specifies the client id of the Azure ManagedIdentity in the case of user assigned identity.</param>
        /// <param name="storageAccountUri">The Cloud Storage Account Uri to use to authenticate using Azure Identity</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            Uri storageAccountUri,
            string managedIdentityClientId = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));

            DefaultAzureCredential defaultAzureCredential;
            if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
            {
                defaultAzureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    { ManagedIdentityClientId = managedIdentityClientId });
            }
            else
            {
                defaultAzureCredential = new DefaultAzureCredential();
            }

            BlobServiceClient blobServiceClient = new BlobServiceClient(storageAccountUri, defaultAzureCredential);

            AzureBlobStorageSinkOptions options = new AzureBlobStorageSinkOptions();
            options.Formatter = formatter;
            if (!string.IsNullOrEmpty(storageContainerName)) options.StorageContainerName = storageContainerName;
            if (!string.IsNullOrEmpty(storageFileName)) options.StorageFileName = storageFileName;
            options.BypassContainerCreationValidation = bypassBlobCreationValidation;
            if (cloudBlobProvider != null) options.CloudBlobProvider = cloudBlobProvider;
            if (!string.IsNullOrEmpty(contentType)) options.ContentType = contentType;
            options.BlobSizeLimitBytes = blobSizeLimitBytes;
            options.RetainedBlobCountLimit = retainedBlobCountLimit;
            options.UseUtcTimezone = useUtcTimeZone;

            try
            {
                AzureBlobStorageSink blobStorageSink = new AzureBlobStorageSink(blobServiceClient, options);

                BatchingOptions batchingOptions = new BatchingOptions
                {
                    BatchSizeLimit = batchPostingLimit ?? DefaultBatchSizeLimit,
                    EagerlyEmitFirstEvent = true,
                    BufferingTimeLimit = period ?? DefaultPeriod,
                };

                return loggerConfiguration.Sink(blobStorageSink, batchingOptions, restrictedToMinimumLevel);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");
                Logger sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink);
            }
        }

        /// <summary>
        /// Adds a sink that writes log events as records in Azure Blob Storage blob (default name 'log.txt') using the given
        /// storage account connection string.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="storageAccountUri">The Cloud Storage Account Uri to use to authenticate using Azure Identity</param>
        /// <param name="managedIdentityClientId">Specifies the client id of the Azure ManagedIdentity in the case of user assigned identity.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required in order to write an event to the sink.</param>
        /// <param name="storageContainerName">Container where the log entries will be written to.</param>
        /// <param name="storageFileName">File name that log entries will be written to.</param>
        /// <param name="outputTemplate"> The template to use for writing log entries. The default is '[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}'</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            Uri storageAccountUri,
            string managedIdentityClientId = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            string outputTemplate = null,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            IFormatProvider formatProvider = null,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUtcTimeZone = false)
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
                managedIdentityClientId,
                restrictedToMinimumLevel,
                storageContainerName,
                storageFileName,
                period,
                batchPostingLimit,
                bypassBlobCreationValidation,
                cloudBlobProvider,
                contentType,
                blobSizeLimitBytes,
                retainedBlobCountLimit,
                useUtcTimeZone);
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
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="bypassBlobCreationValidation">Bypass the exception in case the blob creation fails.</param>
        /// <param name="cloudBlobProvider">Cloud blob provider to get current log blob.</param>
        /// <param name="contentType">The content type to use for the Azure Append Blob.  The default is text/plain.</param>
        /// <param name="blobSizeLimitBytes">The maximum file size to allow before a new one is rolled, expressed in bytes.</param>
        /// <param name="retainedBlobCountLimit">The number of latest blobs to be retained in the container always. Deletes older blobs when this limit is crossed.</param>
        /// <param name="useUtcTimeZone">Use UTC Timezone for logging events.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration AzureBlobStorage(this LoggerSinkConfiguration loggerConfiguration,
            ITextFormatter formatter,
            BlobServiceClient blobServiceClient,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string storageContainerName = null,
            string storageFileName = null,
            TimeSpan? period = null,
            int? batchPostingLimit = null,
            bool bypassBlobCreationValidation = false,
            ICloudBlobProvider cloudBlobProvider = null,
            string contentType = "text/plain",
            long? blobSizeLimitBytes = null,
            int? retainedBlobCountLimit = null,
            bool useUtcTimeZone = false)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (blobServiceClient == null) throw new ArgumentNullException(nameof(blobServiceClient));
            if (blobSizeLimitBytes != null && blobSizeLimitBytes < 1) throw new ArgumentException("Invalid value provided; file size limit must be at least 1 byte, or null.");
            if (retainedBlobCountLimit != null && retainedBlobCountLimit < 1) throw new ArgumentException("Invalid value provided; retained blob count limit must be at least 1 or null.");

            AzureBlobStorageSinkOptions options = new AzureBlobStorageSinkOptions();
            options.Formatter = formatter;
            if (!string.IsNullOrEmpty(storageContainerName)) options.StorageContainerName = storageContainerName;
            if (!string.IsNullOrEmpty(storageFileName)) options.StorageFileName = storageFileName;
            options.BypassContainerCreationValidation = bypassBlobCreationValidation;
            if (cloudBlobProvider != null) options.CloudBlobProvider = cloudBlobProvider;
            if (!string.IsNullOrEmpty(contentType)) options.ContentType = contentType;
            options.BlobSizeLimitBytes = blobSizeLimitBytes;
            options.RetainedBlobCountLimit = retainedBlobCountLimit;
            options.UseUtcTimezone = useUtcTimeZone;

            try
            {
                AzureBlobStorageSink blobStorageSink = new AzureBlobStorageSink(blobServiceClient, options);

                BatchingOptions batchingOptions = new BatchingOptions
                {
                    BatchSizeLimit = batchPostingLimit ?? DefaultBatchSizeLimit,
                    EagerlyEmitFirstEvent = true,
                    BufferingTimeLimit = period ?? DefaultPeriod,
                };

                return loggerConfiguration.Sink(blobStorageSink, batchingOptions, restrictedToMinimumLevel);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine($"Error configuring AzureBlobStorage: {ex}");
                var sink = new LoggerConfiguration().CreateLogger();
                return loggerConfiguration.Sink(sink);
            }
        }
    }
}
