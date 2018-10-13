# Serilog.Sinks.AzureBlobStorage

![Build status](https://dev.azure.com/cloudscope/Serilog/_apis/build/status/Serilog-CI "Build status")

Writes to a file in [Windows Azure Blob Storage](https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs/).

**Package** - [Serilog.Sinks.AzureBlobStorage](http://nuget.org/packages/serilog.sinks.azureblobstorage) | **Platforms** - .NET 4.5, .Net Standard 2.0

```csharp
var storage = CloudStorageAccount.FromConfigurationSetting("MyStorage");

var log = new LoggerConfiguration()
    .WriteTo.AzureBlobStorage(storage)
    .CreateLogger();
```

### JSON configuration

It is possible to configure the sink using [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) by specifying the folder and file name and connection string in `appsettings.json`:

```json
"Serilog": {
  "WriteTo": [
    {"Name": "AzureBlobStorage", "Args": {"storageFolderName": "", "storageFileName": "", "connectionString": ""}}
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

### Acknowledgements

This is a fork of the Serilog [Azure Table storage sink](https://github.com/serilog/serilog-sinks-azuretablestorage).  Thanks 
and acknowledgements to the original authors of that work.