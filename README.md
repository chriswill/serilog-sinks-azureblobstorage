# Serilog.Sinks.AzureBlobStorage

![Build status](https://dev.azure.com/cloudscope/Open%20Source/_apis/build/status/SeriLog-AzureBlobSink%20release "Build status")
[![NuGet Badge](https://buildstats.info/nuget/Serilog.Sinks.AzureBlobStorage)](https://www.nuget.org/packages/Serilog.Sinks.AzureBlobStorage/)

Writes to a file in [Windows Azure Blob Storage](https://azure.microsoft.com/en-us/services/storage/blobs/).

Azure Blob Storage offers [appending blobs](https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs/), which allow you to add content quickly to a single blob without locking it for updates.  For this reason, appending blobs are ideal for logging applications.

The AzureBlobStorage sink appends data to the blob in text format. Here's a sample line:
```
[2018-10-17 23:03:56 INF] Hello World!
```

**Package** - [Serilog.Sinks.AzureBlobStorage](http://nuget.org/packages/serilog.sinks.azureblobstorage) | **Platforms** - .NET 4.5, .Net Standard 2.0

**Usage**
```csharp
var connectionString = CloudStorageAccount.FromConfigurationSetting("MyStorage");

var log = new LoggerConfiguration()
    .WriteTo.AzureBlobStorage(connectionString)
    .CreateLogger();
```

In addition to the storage connection, you can also specify:
* Message line format (default: [{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception})
* Blob container (default: logs)
* Blob filename (default: log.txt)

### Configuration examples

#### Rolling file example

By default, the log file name is logs.txt, but you can add date substitutions to create a rolling file implementation. These are more fully shown in the 
[Unit Test](https://github.com/chriswill/serilog-sinks-azureblobstorage/blob/master/test/Serilog.Sinks.AzureBlobStorage.UnitTest/BlobNameFactoryUT.cs) 
project. But as an example, you can create a log file name like this: {yyyy}/{MM}/{dd}/log.txt

```csharp
  .WriteTo.AzureBlobStorage(connectionString, Serilog.Events.LogEventLevel.Information, null, "{yyyy}/{MM}/{dd}/log.txt")
```

On December 15, 2018 (when this was written), log files would appear to be in a folder structure as shown below:

```

\2018
-----\12
      ----\15
            log.txt

```

In the file name, the values must appear in descending order, e.g.: yy MM dd hh mm, although it is not required to include all date elements.

#### Batch posting example

By default, whenever there is a new event to post, the Azure Blob Storage sink will send it to Azure storage.  For cost-management or performance reasons, you can
choose to "batch" the posting of new log events.

You should create the sink by calling the [AzureBatchingBlobStorageSink](https://github.com/chriswill/serilog-sinks-azureblobstorage/blob/master/src/Serilog.Sinks.AzureBlobStorage/Sinks/AzureBlobStorage/AzureBatchingBlobStorageSink.cs) class, which inherits from PeriodicBatchingSink.

An example configuration is:
```csharp
  .WriteTo.AzureBlobStorage(connectionString, Serilog.Events.LogEventLevel.Information, null, null, null, true, TimeSpan.FromSeconds(15), 10)
```
This configuration would post a new batch of events every 15 seconds, unless there were 10 or more events to post, in which case they would post before the time limit.


### JSON configuration

It is possible to configure the sink using [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) by specifying the folder and file name and connection string in `appsettings.json`:

```json
"Serilog": {
  "WriteTo": [
    {"Name": "AzureBlobStorage", "Args": {"connectionString": "", "storageFolderName": "", "storageFileName": ""}}
  ]
}
```

JSON configuration must be enabled using `ReadFrom.Configuration()`; see the [documentation of the JSON configuration package](https://github.com/serilog/serilog-settings-configuration) for details.

### XML `<appSettings>` configuration

To use the file sink with the [Serilog.Settings.AppSettings](https://github.com/serilog/serilog-settings-appsettings) package, first install that package if you haven't already done so:

```powershell
Install-Package Serilog.Settings.AppSettings
```

Instead of configuring the logger in code, call `ReadFrom.AppSettings()`:

```csharp
var log = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

In your application's `App.config` or `Web.config` file, specify the file sink assembly and required path format under the `<appSettings>` node:

```xml
<configuration>
  <appSettings>
    <add key="serilog:using:AzureBlobStorage" value="Serilog.Sinks.AzureBlobStorage" />
    <add key="serilog:write-to:AzureBlobStorage.connectionString" value="DefaultEndpointsProtocol=https;AccountName=ACCOUNT_NAME;AccountKey=KEY;EndpointSuffix=core.windows.net" />
    <add key="serilog:write-to:AzureBlobStorage.formatter" value="Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact" />
```

### A note about Unit Testing

Unfortunately the Azure Storage emulator does not support append blobs, so I'm omitted unit tests from this project.  I'd love to have unit tests,
but I'd like to have them be able to run on Azure Dev Ops 
[hosted build agents](https://github.com/Microsoft/azure-pipelines-image-generation/blob/master/images/win/Vs2017-Server2016-Readme.md).  Suggestions?

### Acknowledgements

This is a fork of the Serilog [Azure Table storage sink](https://github.com/serilog/serilog-sinks-azuretablestorage).  Thanks 
and acknowledgements to the original authors of that work.
