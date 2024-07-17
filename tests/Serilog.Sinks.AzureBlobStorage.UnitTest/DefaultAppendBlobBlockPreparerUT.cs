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
        private readonly DefaultAppendBlobBlockPreparer defaultAppendBlobBlockPreparer;
        private readonly ITextFormatter defaultTextFormatter;
        private readonly IReadOnlyCollection<LogEvent> emptyLogEventEnumerable;

        private readonly LogEvent tooLargeLogEvent= new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, MessageTemplate.Empty, new[] { new LogEventProperty("BigProp", new Serilog.Events.ScalarValue(new string('*', 1024 * 1024 * 6))) });
        private readonly LogEvent largeLogEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, MessageTemplate.Empty, new[] { new LogEventProperty("BigProp", new Serilog.Events.ScalarValue(new string ('*', 1024 * 512))) });

        public DefaultAppendBlobBlockPreparerUT()
        {
            defaultAppendBlobBlockPreparer = new DefaultAppendBlobBlockPreparer();
            defaultTextFormatter = new Formatting.Json.JsonFormatter();
            emptyLogEventEnumerable = new List<LogEvent>();
        }

        [Fact(DisplayName = "Should throw validation exception due to missing ITextFormatter instance.")]
        public void MissingITextFormatterInstance()
        {
            //TODO: This test is not valid as the DefaultAppendBlobBlockPreparer does not validate the ITextFormatter instance.
            //Assert.Throws<ArgumentNullException>(() => defaultAppendBlobBlockPreparer.PrepareAppendBlocks(null, emptyLogEventEnumerable));
        }

        [Fact(DisplayName = "Should throw validation exception due to missing IEnumerable<LogEvent> instance.")]
        public void MissingIEnumerableLogEventsInstance()
        {
            Assert.Throws<ArgumentNullException>(() => defaultAppendBlobBlockPreparer.PrepareAppendBlocks(defaultTextFormatter, null));
        }

        [Fact(DisplayName = "Should return an empty IEnumerable<string> when an empty IEnumberable<LogEvent> goes in.")]
        public void ReturnEmptyResultWhenInputIsEmpty()
        {
            IEnumerable<string> preparedResult = defaultAppendBlobBlockPreparer.PrepareAppendBlocks(defaultTextFormatter, emptyLogEventEnumerable);
            Assert.Empty(preparedResult);
        }

        [Fact(DisplayName = "Should drop LogEvents that when formatted are greater than 4MB")]
        public void DropTooLargeLogEvents()
        {
            LogEvent[] logEvents = new[] { tooLargeLogEvent };

            List<string> preparedResult = defaultAppendBlobBlockPreparer.PrepareAppendBlocks(defaultTextFormatter, logEvents).ToList();

            Assert.Empty(preparedResult);
        }

        [Fact(DisplayName = "Should return multiple blocks when the log events overflow the 4mb block size.")]
        public void ReturnMultipleBlocksWhenLogEventsOverflow4Mb()
        {
            LogEvent[] logEvents = new[] { largeLogEvent, largeLogEvent , largeLogEvent , largeLogEvent , largeLogEvent , largeLogEvent , largeLogEvent, largeLogEvent, largeLogEvent };

            List<string> preparedResult = defaultAppendBlobBlockPreparer.PrepareAppendBlocks(defaultTextFormatter, logEvents).ToList();

            Assert.NotEmpty(preparedResult);
            Assert.True(preparedResult.Count == 2);
        }

        [Fact(DisplayName = "Should return single block when the log events stay below the formatted size of 4mb.")]
        public void ReturnSinglelocksWhenLogEventsStayBelow4MbLimit()
        {
            LogEvent[] logEvents = new[] { largeLogEvent, largeLogEvent, largeLogEvent};

            List<string> preparedResult = defaultAppendBlobBlockPreparer.PrepareAppendBlocks(defaultTextFormatter, logEvents).ToList();

            Assert.NotEmpty(preparedResult);
            Assert.True(preparedResult.Count == 1);
        }

    }
}
