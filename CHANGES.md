# Changes

### 3.2.0 10/18/2023
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
