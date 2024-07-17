using System.Collections.Generic;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.AzureBlobStorage
{
    public interface IAppendBlobBlockPreparer
    {
        IEnumerable<string> PrepareAppendBlocks(ITextFormatter textFormatter, IReadOnlyCollection<LogEvent> logEvents);
    }
}