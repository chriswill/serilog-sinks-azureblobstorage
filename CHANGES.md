# Changes

### 4.0.5 10/30/2024
- Updated Nugets to the latest versions for all dependencies.  There was a security vulnerability in System.Text.Json that has been addressed by updating.

### 4.0.3 08/20/2024
- Changed constructors using a managedIdentityClientId to make it optional.  If not provided, the sink will use the default Azure credential chain.
- Updated Nugets for Serilog and Azure.Storage.Blobs to the latest versions.

### 4.0.2 07/23/2024
- Updated System.Text.Json to 8.0.4 to address [CVE-2024-30105](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w).
- Replaced Microsoft.Extensions.Hosting with Microsoft.Extensions.Configuration.Abstractions.

### 4.0.1 07/18/2024
- Updated references so that Microsoft.Bcl.AsyncInterfaces is only required for netstandard2.0.
- Updated/clarified extension method that supports Sas token authentication.

### 4.0.0 07/17/2024
- Updated to Serilog 4.0.0 and implemented support for Serilog native IBatchedLogEventSink. All usage of AzureBlobStorage is now batched on a default 2 second emit interval. The first log event is written immediately.
- Implemented support for including the log event level (Information, Warning, etc) in the file name template.  This is done by including the `Level` property in the template.  For example, `Log-{yyyy}-{MM}-{dd}-{Level}.txt` will create files like `Log-2024-07-17-Information.txt`.
- Fixed support for deleting old files by implementing Regex matching instead of DateTime parsing in the delete routine.
- Added console app sample to demonstrate the new features.
- This major update may require you to alter your configuration settings where you define usage of this sink.

### 3.3.2 07/13/2024
- Updated Azure.Identity to fix CVE-2024-35255.  The next release will be a major version update to adopt Serilog 4.0.0 and IBatchedLogEventSink.

### 3.3.1 04/28/2024
- Updated to current Nuget packages, including Azure.Identity 1.11.2.  
- Adopted [pull request #110](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/110) by @Marien-OV, which implements improvements for batching and using the BlobClient.
- 
### 3.3.0 10/18/2023
- Updated to current Nuget packages, including Serilog 3.0.1.  
- Adopted [pull request #102](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/102) by @FDUdannychen, which allows for setting the blob content type. Default is text/plain.
- Adopted [pull request #103](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/103) by @jamesSampica, which writes exceptions to Serilog's selflog.

### 3.1.3 8/2/2022
- No code changes, changed Azure.Storage.Blobs to version 12.13.0 to address security vulnerability.
- 
### 3.1.2 4/8/2022
- No code changes, just adopted the standard logo for Serilog Sink nuget packages.

### 3.1.1 2/23/2022

- Adopted [pull request #86](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/86) by @dsbut that added configuration parameter for logging in UTC format.
- Adopted [pull request #87](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/87) by @chriswill that added configuration support for using a named connection string.
- Implemented IBatchedLogEventSink in AzureBatchingBlobStorageSink to adopt current approach for Serilog Periodic Batching.

### 3.0.3 9/6/2021

- Adopted [pull request #85](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/85) by @throck95 that added support for logging in UTC format.

### 3.0.2 7/5/2021

- Revised code so that it could be compiled as netstandard2.0.

### 3.0.1 5/31/2021

- Adopted [pull request](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/79) by @KrishnaKole that added support for Azure Managed Identities.

### 3.0.0 5/20/2021

- Adopted [pull request](https://github.com/chriswill/serilog-sinks-azureblobstorage/pull/78) by @vplauzon that uses the Azure.Storage.Blob library. If you are still using WindowsAzure.Storage, please use version 2.1.2 or earlier.

### 2.1.2 4/20/2021

- Adopted pull request by @CCubbage to add SyncContextSafeWait to DeleteArchivedBlobsAsync
- This is the last anticipated release before a move to the new Microsoft storage libraries

### 2.1.1 2/4/2021

- Adopted pull request by @MarcDeRaedt to call FetchAttributesAsync to determine blob size for rollover

### 2.1.0 12/16/2020

- Adopted pull request by @AdarshGupta to limit the number of blobs retained in a container

### 2.0.0 8/29/2020

- Adopted pull request by @stackag to roll blob on file size limit being reached
- Adopted pull request by @adementjeva to allow repeated date format values in file name
- Removed support for net452. Supports netstandard2.0 only.
- Updated all references, including references for Microsoft.Azure.Storage.Blob

### 1.4.0 6/14/2019

- Changed format of .Net Standard version to netstandard2.0. Sink was not emitting events to storage
  under previous netstandard1.3, which seemed to be a result of our adoption of Microsoft.Azure.Storage.Blob.

### 1.3.0 5/3/2019

- Changed nuget dependency reference from WindowsAzure.Storage to Microsoft.Azure.Storage.Blob.
- Changed minimum .Net Framework version to 4.5.2 to support Microsoft.Azure.Storage.Blob.

### 1.2.3 3/27/2019

- Reverted back to WindowsAzure.Storage.

### 1.2.1 3/25/2019

- Changed nuget dependency reference from WindowsAzure.Storage to Microsoft.Azure.Storage.Blob for .Net Core projects. .Net Framework apps seem to have trouble referencing Microsoft.Azure.Storage.Blob even though it lists support for .Net Framework 4.5.
- Also updated the minimum Serilog reference to 2.7.1 because it has many fewer dependencies for both .Net Core and Framework.

### 1.1.1 2/26/2019

- Updated Readme.md to reflect new configuration change (folderName ==> containerName). The 1.0.4 release should have been incremented to 1.1.0 because it introduced a possible breaking change to configuration settings, so we're catching up now.

### 1.0.4 2/22/2019

- Renamed references to "folder" with "container" in source property names and VS doc for greater clarity

### 1.0.2 1/24/2019

- Sink refactoring, with changes to support AppendBlob size limits

### 1.0.1 1/22/2019

- Fixed issue with file name template in the batching provider
- Changed versioning back to major.minor.patch format
- Minor solution cleanup

### 1.0.0.1 12/10/2018

- Fixed a bug where multiple logging statements would post only the first statement

### 0.8.0 10/12/2018

- Initial beta release
