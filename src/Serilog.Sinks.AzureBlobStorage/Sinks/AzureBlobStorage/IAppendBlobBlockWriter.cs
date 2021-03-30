using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Serilog.Sinks.AzureBlobStorage
{
    public interface IAppendBlobBlockWriter
    {
        Task WriteBlocksToAppendBlobAsync(AppendBlobClient appendBlobClient, IEnumerable<string> blocks);
    }
}