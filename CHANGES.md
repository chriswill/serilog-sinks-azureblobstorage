# Changes

### 2.1.0 12/16/2020
* Adopted pull request by @AdarshGupta limit the number of blobs retained in a container

### 2.0.0 8/29/2020
* Adopted pull request by @stackag to roll blob on file size limit being reached
* Adopted pull request by @adementjeva to allow repeated date format values in file name
* Removed support for net452. Supports netstandard2.0 only.
* Updated all references, including references for Microsoft.Azure.Storage.Blob

### 1.4.0 6/14/2019
* Changed format of .Net Standard version to netstandard2.0. Sink was not emitting events to storage 
under previous netstandard1.3, which seemed to be a result of our adoption of Microsoft.Azure.Storage.Blob.

### 1.3.0 5/3/2019
* Changed nuget dependency reference from WindowsAzure.Storage to Microsoft.Azure.Storage.Blob.
* Changed minimum .Net Framework version to 4.5.2 to support Microsoft.Azure.Storage.Blob.

### 1.2.3 3/27/2019
* Reverted back to WindowsAzure.Storage.

### 1.2.1 3/25/2019
* Changed nuget dependency reference from WindowsAzure.Storage to Microsoft.Azure.Storage.Blob for .Net Core projects. .Net Framework apps seem to have trouble referencing Microsoft.Azure.Storage.Blob even though it lists support for .Net Framework 4.5.
* Also updated the minimum Serilog reference to 2.7.1 because it has many fewer dependencies for both .Net Core and Framework.

### 1.1.1 2/26/2019
* Updated Readme.md to reflect new configuration change (folderName ==> containerName). The 1.0.4 release should have been incremented to 1.1.0 because it introduced a possible breaking change to configuration settings, so we're catching up now.

### 1.0.4 2/22/2019
* Renamed references to "folder" with "container" in source property names and VS doc for greater clarity

### 1.0.2 1/24/2019
* Sink refactoring, with changes to support AppendBlob size limits

### 1.0.1 1/22/2019
* Fixed issue with file name template in the batching provider
* Changed versioning back to major.minor.patch format
* Minor solution cleanup

### 1.0.0.1 12/10/2018
* Fixed a bug where multiple logging statements would post only the first statement

### 0.8.0 10/12/2018
* Initial beta release
