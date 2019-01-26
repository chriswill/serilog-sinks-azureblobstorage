using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Serilog.Sinks.AzureBlobStorage
{
    internal class DefaultAppendBlobBlockWriter : IAppendBlobBlockWriter
    {

        public async Task WriteBlocksToAppendBlobAsync(CloudAppendBlob cloudAppendBlob, IEnumerable<string> blocks)
        {
            if (cloudAppendBlob == null)
            {
                throw new ArgumentNullException(nameof(cloudAppendBlob));
            }

            if (blocks == null)
            {
                throw new ArgumentNullException(nameof(blocks));
            }

            foreach (string blockContent in blocks)
            {
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(blockContent)))
                {
                    try
                    {
                        await cloudAppendBlob.AppendBlockAsync(stream);
                    }
                    catch (StorageException ex)
                    {
                        Debugging.SelfLog.WriteLine($"Exception {ex} thrown while trying to append a block. Http response code {ex.RequestInformation?.HttpStatusCode} and error code {ex.RequestInformation?.ErrorCode}. If this is the second or later block in this batch there might be duplicate log entries written due to the retry mechanism.");
                    }
                    catch (Exception ex)
                    {
                        Debugging.SelfLog.WriteLine($"Exception {ex} thrown while trying to append a block. If this is the second or later block in this batch there might be duplicate log entries written due to the retry mechanism.");
                    }
                }
            }
        }

    }
}
