using System;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class BlobNameFactoryUT
    {
        [Fact(DisplayName = "Should throw validation exception due to invalid format characters.")]
        public void InvalidFormatCharacters()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));

            Assert.Throws<ArgumentException>(() => new BlobNameFactory(@"{xx}\name.txt"));
        }

        [Fact(DisplayName = "Should parse year month even if format characters out of order.")]
        public void OutOfOrderFormatCharacters()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));

            var bn = new BlobNameFactory(@"{yyyy}/{dd}/{MM}/name.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("2018/05/11/name.txt", result);
        }

        [Fact(DisplayName = "Should result in same filename.")]
        public void SameName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("samename.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("samename.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month folder with day as filename.")]
        public void YearMonthFolderDayName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/2018/11/05.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with hours as filename.")]
        public void YearMonthDayFolderHoursName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/{HH}.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/2018/11/05/08.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day, hours folder with minutes as filename.")]
        public void YearMonthDayHoursFolderMinutesName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/{HH}/{mm}.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/2018/11/05/08/30.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, and day into single folder with hours as filename.")]
        public void YearMonthDayOneFolderHoursName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyyMMdd}/{HH}.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/20181105/08.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with static filename.")]
        public void YearMonthDayFolderStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/logs.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/2018/11/05/logs.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder by UTC Date with static filename.")]
        public void YearMonthDayUTCFolderStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/{HH}/logs.txt");

            var result = bn.GetBlobName(dtoToApply, useUTCTimeZone: true);

            Assert.Equal("webhook/2018/11/05/13/logs.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with static filename.")]
        public void YearMonthDayFolderYearMonthDayFileStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/logs-{yyyy}-{MM}-{dd}.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/2018/11/05/logs-2018-11-05.txt", result);
        }

        [Theory(DisplayName = "Returns the blob name format which is supported by DateTime Parser to identify blobs created by the logger.")]
        [InlineData("{yyyy}/{dd}/{MM}/name.txt", "''yyyy'/'dd'/'MM'/name.txt'")]
        [InlineData("samename.txt", "'samename.txt'")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}.txt", "'webhook/'yyyy'/'MM'/'dd'.txt'")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/{HH}.txt", "'webhook/'yyyy'/'MM'/'dd'/'HH'.txt'")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/{HH}/{mm}.txt", "'webhook/'yyyy'/'MM'/'dd'/'HH'/'mm'.txt'")]
        [InlineData("webhook/{yyyyMMdd}/{HH}.txt", "'webhook/'yyyyMMdd'/'HH'.txt'")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/logs.txt", "'webhook/'yyyy'/'MM'/'dd'/logs.txt'")]
        [InlineData("webhook/{yyyy}/{MM}/{dd}/logs-{yyyy}-{MM}-{dd}.txt", "'webhook/'yyyy'/'MM'/'dd'/logs-'yyyy'-'MM'-'dd'.txt'")]
        [InlineData("{yyyy}/{dd}/{MM}/application logs.txt", "''yyyy'/'dd'/'MM'/application logs.txt'")]
        public void GetBlobNameFormat_ReturnsBlobNameFormatAsPerDateTimeParser(string blobName, string expectedResult)
        {
            var blobNameFactory = new BlobNameFactory(blobName);
            var actualResult = blobNameFactory.GetBlobNameFormat();

            Assert.Equal(expectedResult, actualResult);
        }
    }
}
