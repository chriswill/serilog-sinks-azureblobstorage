using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.AzureBlobStorage
{
    internal class DefaultAppendBlobBlockPreparer : IAppendBlobBlockPreparer
    {
        private const int MaxAppendBlobBlockSize = 1024 * 1024 * 4;

        public IEnumerable<string> PrepareAppendBlocks(ITextFormatter textFormatter, IEnumerable<LogEvent> logEvents)
        {
            if (textFormatter == null)
            {
                throw new ArgumentNullException(nameof(textFormatter));
            }

            if (logEvents == null)
            {
                throw new ArgumentNullException(nameof(logEvents));
            }

            List<string> blockContents = new List<string>();
            StringBuilder currentBlockContent = new StringBuilder();
            int currentBlockSize = 0;
            foreach (LogEvent logEvent in logEvents)
            {
                using (StringWriter tempStringWriter = new StringWriter())
                {
                    try
                    {
                        textFormatter.Format(logEvent, tempStringWriter);
                        tempStringWriter.Flush();
                    }
                    catch (Exception ex)
                    {
                        Debugging.SelfLog.WriteLine($"Exception {ex} thrown during logEvent formatting. The log event will be dropped.");
                        continue;
                    }

                    int logEventSize = Encoding.UTF8.GetByteCount(tempStringWriter.ToString());
                    if (logEventSize > MaxAppendBlobBlockSize)
                    {
                        Debugging.SelfLog.WriteLine($"LogEvent is larger than the allowed max append blob block size. The log event cannot be logged and will be dropped. The log event: {tempStringWriter}");
                        continue;
                    }

                    if (logEventSize + currentBlockSize > MaxAppendBlobBlockSize)
                    {
                        //The log event does not fit in the max block size. Cut off the current block and start a new block
                        blockContents.Add(currentBlockContent.ToString());
                        currentBlockContent.Clear();
                        currentBlockSize = 0;
                    }

                    //Add the log event to the block
                    currentBlockContent.Append(tempStringWriter);
                    currentBlockSize += logEventSize;
                }
            }

            //in case of an empty IEnumerable<LogEvent> parameter there are no blocks to write, skip creating an empty block.
            if (currentBlockSize != 0)
            {
                blockContents.Add(currentBlockContent.ToString());
            }
            

            return blockContents;
        }

        
    }
}
