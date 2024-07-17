using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class BlobNameFactoryUT
    {
        [Fact(DisplayName = "Should throw validation exception due to invalid format characters.")]
        public void InvalidFormatCharacters()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            BlobNameFactory bn = new BlobNameFactory(@"{xx}\name.txt");

            Assert.Throws<ArgumentException>(() => bn.GetBlobName(dtoToApply, LogEventLevel.Information));
        }

        [Fact(DisplayName = "Should parse file name using custom properties.")]
        public void UsesSuppliedProperties()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));

            LogEventPropertyValue value = new ScalarValue("foo");

            Dictionary<string, LogEventPropertyValue> properties =
                new Dictionary<string, LogEventPropertyValue>();
            properties.Add("xx", value);

            BlobNameFactory bn = new BlobNameFactory(@"{xx}\name.txt");

            string result = bn.GetBlobName(dtoToApply, LogEventLevel.Information, new ReadOnlyDictionary<string, LogEventPropertyValue>(properties));

            Assert.Equal("foo\\name.txt", result);
        }

        [Fact(DisplayName = "Should parse year month even if format characters out of order.")]
        public void OutOfOrderFormatCharacters()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));

            var bn = new BlobNameFactory("{yyyy}/{dd}/{MM}/name.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("2018/05/11/name.txt", result);
        }

        [Fact(DisplayName = "Should result in same filename.")]
        public void SameName()
        {
            DateTimeOffset dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            BlobNameFactory bn = new BlobNameFactory("samename.txt");

            string result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("samename.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month folder with day as filename.")]
        public void YearMonthFolderDayName()
        {
            DateTimeOffset dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            BlobNameFactory bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}.txt");

            string result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("webhook/2018/11/05.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with hours as filename.")]
        public void YearMonthDayFolderHoursName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/{HH}.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("webhook/2018/11/05/08.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day, hours folder with minutes as filename.")]
        public void YearMonthDayHoursFolderMinutesName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/{HH}/{mm}.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("webhook/2018/11/05/08/30.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, and day into single folder with hours as filename.")]
        public void YearMonthDayOneFolderHoursName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyyMMdd}/{HH}.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("webhook/20181105/08.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with static filename.")]
        public void YearMonthDayFolderStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/logs.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("webhook/2018/11/05/logs.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder by UTC Date with static filename.")]
        public void YearMonthDayUTCFolderStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/{HH}/logs.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information, useUtcTimeZone: true);

            Assert.Equal("webhook/2018/11/05/13/logs.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with static filename.")]
        public void YearMonthDayFolderYearMonthDayFileStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/logs-{yyyy}-{MM}-{dd}.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("webhook/2018/11/05/logs-2018-11-05.txt", result);
        }

        [Theory(DisplayName = "Returns the blob name format which is supported by DateTime Parser to identify blobs created by the logger.")]
        [InlineData("{yyyy}/{dd}/{MM}/name.txt", "\\d{4}/\\d{2}/\\d{2}/name\\.txt")]
        [InlineData("samename.txt", "samename\\.txt")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}.txt", "webhook/\\d{4}/\\d{2}/\\d{2}\\.txt")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/{HH}.txt", "webhook/\\d{4}/\\d{2}/\\d{2}/\\d{2}\\.txt")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/{HH}/{mm}.txt", "webhook/\\d{4}/\\d{2}/\\d{2}/\\d{2}/\\d{2}\\.txt")]
        [InlineData("webhook/{yyyyMMdd}/{HH}.txt", "webhook/\\d{8}/\\d{2}\\.txt")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/logs.txt", "webhook/\\d{4}/\\d{2}/\\d{2}/logs\\.txt")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/logs-{yyyy}-{MM}-{dd}.txt", "webhook/\\d{4}/\\d{2}/\\d{2}/logs-\\d{4}-\\d{2}-\\d{2}\\.txt")]
        [InlineData("{yyyy}/{dd}/{MM}/applicationlogs.txt", "\\d{4}/\\d{2}/\\d{2}/applicationlogs\\.txt")]
        [InlineData("{xx}/name.txt", "\\S*/name\\.txt")]
        [InlineData("{xx}/{Level}-name.txt", "\\S*/\\S*-name\\.txt")]
        //@"{xx}\name.txt"
        public void GetBlobNameFormat_ReturnsBlobNameFormatAsPerDateTimeParser(string blobName, string expectedResult)
        {
            BlobNameFactory blobNameFactory = new BlobNameFactory(blobName);
            string regex = blobNameFactory.GetBlobRegex();
            Assert.Equal(expectedResult, regex);
        }

        [Fact(DisplayName = "Should create regex based on name format")]
        public void CanCreateAppropriateRegex()
        {
            BlobNameFactory blobNameFactory = new BlobNameFactory("{xx}-name.txt");
            string actualResult = blobNameFactory.GetBlobRegex();
            Assert.Equal("\\S*-name\\.txt", actualResult);
        }
    }
}
