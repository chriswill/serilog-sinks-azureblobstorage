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

using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

namespace Serilog.Sinks.AzureBlobStorage.AzureBlobProvider
{
    public interface ICloudBlobProvider
    {
        Task<CloudAppendBlob> GetCloudBlobAsync(CloudBlobClient cloudBlobClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation, long? blobSizeLimitBytes = null);
    }
}
