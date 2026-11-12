﻿using System;
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

        public IEnumerable<string> PrepareAppendBlocks(ITextFormatter textFormatter, IReadOnlyCollection<LogEvent> logEvents)
        {
            if (textFormatter == null)
            {
                throw new ArgumentNullException(nameof(textFormatter));
            }

            if (logEvents == null)
            {
                throw new ArgumentNullException(nameof(logEvents));
            }

            return PrepareAppendBlocksIterator(textFormatter, logEvents);
        }

        // Blocks are produced one at a time, so that only the block being written is held in memory.
        private static IEnumerable<string> PrepareAppendBlocksIterator(ITextFormatter textFormatter, IReadOnlyCollection<LogEvent> logEvents)
        {
            StringBuilder currentBlockContent = new StringBuilder();
            int currentBlockSize = 0;
            foreach (LogEvent logEvent in logEvents)
            {
                string formattedLogEvent;
                try
                {
                    using (StringWriter tempStringWriter = new StringWriter())
                    {
                        textFormatter.Format(logEvent, tempStringWriter);
                        tempStringWriter.Flush();
                        formattedLogEvent = tempStringWriter.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Debugging.SelfLog.WriteLine("Exception {0} thrown during logEvent formatting. The log event will be dropped.", ex);
                    continue;
                }

                int logEventSize = Encoding.UTF8.GetByteCount(formattedLogEvent);
                if (logEventSize > MaxAppendBlobBlockSize)
                {
                    Debugging.SelfLog.WriteLine("LogEvent is larger than the allowed max append blob block size. The log event cannot be logged and will be dropped. The log event: {0}", formattedLogEvent);
                    continue;
                }

                if (logEventSize + currentBlockSize > MaxAppendBlobBlockSize)
                {
                    //The log event does not fit in the max block size. Cut off the current block and start a new block
                    yield return currentBlockContent.ToString();
                    currentBlockContent.Clear();
                    currentBlockSize = 0;
                }

                //Add the log event to the block
                currentBlockContent.Append(formattedLogEvent);
                currentBlockSize += logEventSize;
            }

            //in case of an empty IEnumerable<LogEvent> parameter there are no blocks to write, skip creating an empty block.
            if (currentBlockSize != 0)
            {
                yield return currentBlockContent.ToString();
            }
        }
    }
}
