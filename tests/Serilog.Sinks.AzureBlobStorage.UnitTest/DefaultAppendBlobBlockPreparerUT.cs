using System;
using System.Collections.Generic;
using System.Linq;
using Serilog.Events;
using Serilog.Formatting;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class DefaultAppendBlobBlockPreparerUT
    {
        private readonly DefaultAppendBlobBlockPreparer _defaultAppendBlobBlockPreparer;
        private readonly ITextFormatter _defaultTextFormatter;
        private readonly IEnumerable<LogEvent> _emptyLogEventEnumerable;

        private readonly LogEvent _tooLargeLogEvent= new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, MessageTemplate.Empty, new[] { new LogEventProperty("BigProp", new Serilog.Events.ScalarValue(new string('*', 1024 * 1024 * 6))) });
        private readonly LogEvent _largeLogEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, MessageTemplate.Empty, new[] { new LogEventProperty("BigProp", new Serilog.Events.ScalarValue(new string ('*', 1024 * 512))) });

        public DefaultAppendBlobBlockPreparerUT()
        {
            _defaultAppendBlobBlockPreparer = new DefaultAppendBlobBlockPreparer();
            _defaultTextFormatter = new Serilog.Formatting.Json.JsonFormatter();
            _emptyLogEventEnumerable = Enumerable.Empty<LogEvent>();
        }

        [Fact(DisplayName = "Should throw validation exception due to missing ITextFormatter instance.")]
        public void MissingITextFormatterInstance()
        {
            Assert.Throws<ArgumentNullException>(() => _defaultAppendBlobBlockPreparer.PrepareAppendBlocks(null, _emptyLogEventEnumerable));
        }

        [Fact(DisplayName = "Should throw validation exception due to missing IEnumerable<LogEvent> instance.")]
        public void MissingIEnumerableLogEventsInstance()
        {
            Assert.Throws<ArgumentNullException>(() => _defaultAppendBlobBlockPreparer.PrepareAppendBlocks(_defaultTextFormatter, null));
        }

        [Fact(DisplayName = "Should return an empty IEnumerable<string> when an empty IEnumberable<LogEvent> goes in.")]
        public void ReturnEmptyResultWhenInputIsEmpty()
        {
            var preparedResult = _defaultAppendBlobBlockPreparer.PrepareAppendBlocks(_defaultTextFormatter, _emptyLogEventEnumerable);

            Assert.Empty(preparedResult);
        }

        [Fact(DisplayName = "Should drop LogEvents that when formatted are greater than 4MB")]
        public void DropTooLargeLogEvents()
        {
            var logEvents = new[] { _tooLargeLogEvent };

            var preparedResult = _defaultAppendBlobBlockPreparer.PrepareAppendBlocks(_defaultTextFormatter, logEvents);

            Assert.Empty(preparedResult);
        }

        [Fact(DisplayName = "Should return multiple blocks when the log events overflow the 4mb block size.")]
        public void ReturnMultipleBlocksWhenLogEventsOverflow4MB()
        {
            var logEvents = new[] { _largeLogEvent, _largeLogEvent , _largeLogEvent , _largeLogEvent , _largeLogEvent , _largeLogEvent , _largeLogEvent, _largeLogEvent, _largeLogEvent };

            var preparedResult = _defaultAppendBlobBlockPreparer.PrepareAppendBlocks(_defaultTextFormatter, logEvents);

            Assert.NotEmpty(preparedResult);
            Assert.True(preparedResult.Count() == 2);
        }

        [Fact(DisplayName = "Should return single block when the log events stay below the formatted size of 4mb.")]
        public void ReturnSinglelocksWhenLogEventsStayBelow4MBLimit()
        {
            var logEvents = new[] { _largeLogEvent, _largeLogEvent, _largeLogEvent};

            var preparedResult = _defaultAppendBlobBlockPreparer.PrepareAppendBlocks(_defaultTextFormatter, logEvents);

            Assert.NotEmpty(preparedResult);
            Assert.True(preparedResult.Count() == 1);
        }

    }
}
