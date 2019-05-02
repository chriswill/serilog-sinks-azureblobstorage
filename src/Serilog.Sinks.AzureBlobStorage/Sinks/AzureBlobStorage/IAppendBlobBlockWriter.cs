using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Storage.Blob;

namespace Serilog.Sinks.AzureBlobStorage
{
    public interface IAppendBlobBlockWriter
    {
        Task WriteBlocksToAppendBlobAsync(CloudAppendBlob cloudAppendBlob, IEnumerable<string> blocks);
    }
}