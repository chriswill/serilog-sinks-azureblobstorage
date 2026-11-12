﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Specialized;

namespace Serilog.Sinks.AzureBlobStorage
{
    public class DefaultAppendBlobBlockWriter : IAppendBlobBlockWriter
    {
        /// <summary>
        /// Appends the blocks to the blob. A failure is reported to Serilog's SelfLog and then rethrown,
        /// so that Serilog retries the batch.
        /// </summary>
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
                        await appendBlobClient.AppendBlockAsync(stream).ConfigureAwait(false);
                    }
                    catch (RequestFailedException ex)
                    {
                        Debugging.SelfLog.WriteLine("Exception {0} thrown while trying to append a block. Http response code {1} and error code {2}. If this is the second or later block in this batch there might be duplicate log entries written due to the retry mechanism.", ex, ex.Status, ex.ErrorCode);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debugging.SelfLog.WriteLine("Exception {0} thrown while trying to append a block. If this is the second or later block in this batch there might be duplicate log entries written due to the retry mechanism.", ex);
                        throw;
                    }
                }
            }
        }
    }
}
