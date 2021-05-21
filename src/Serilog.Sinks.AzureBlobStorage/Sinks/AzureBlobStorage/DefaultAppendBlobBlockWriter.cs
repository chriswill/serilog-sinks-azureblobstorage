using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Serilog.Sinks.AzureBlobStorage
{
    public class DefaultAppendBlobBlockWriter : IAppendBlobBlockWriter
    {
        public async Task WriteBlocksToAppendBlobAsync(AppendBlobClient appendBlobClient, IEnumerable<string> blocks)
        {
            if (appendBlobClient == null)
            {
                throw new ArgumentNullException(nameof(appendBlobClient));
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
                        await appendBlobClient.AppendBlockAsync(stream);
                    }
                    catch (RequestFailedException ex)
                    {
                        Debugging.SelfLog.WriteLine($"Exception {ex} thrown while trying to append a block. Http response code {ex.Status} and error code {ex.ErrorCode}. If this is the second or later block in this batch there might be duplicate log entries written due to the retry mechanism.");
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
