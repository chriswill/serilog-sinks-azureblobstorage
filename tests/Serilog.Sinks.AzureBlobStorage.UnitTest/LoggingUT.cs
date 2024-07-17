using System;
using Serilog.Events;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class LoggingUT
    {
        
        //[Fact(DisplayName = "Should throw validation exception due to invalid format characters.")]
        //public void InvalidFormatCharacters()
        //{
        //    var log = new LoggerConfiguration()
        //        .WriteTo.AzureBlobStorage(ConnectionString)
        //        .CreateLogger();
        //}

        [Fact(DisplayName = "Should throw validation exception due to format characters not accepted")]
        public void OutOfOrderFormatCharacters()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            BlobNameFactory bn = new BlobNameFactory(@"{xx}\name.txt");

            Assert.Throws<ArgumentException>(() => bn.GetBlobName(dtoToApply));
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

        [Fact(DisplayName = "Should parse into year, month, day and hours into single folder with level as filename.")]
        public void LevelName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyyMMddHH}/{Level}.txt");

            var result = bn.GetBlobName(dtoToApply, LogEventLevel.Information);

            Assert.Equal("webhook/2018110508/Information.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with static filename.")]
        public void YearMonthDayFolderStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/logs.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/2018/11/05/logs.txt", result);
        }

        [Fact(DisplayName = "Should parse into year, month, day folder with static filename.")]
        public void YearMonthDayFolderYearMonthDayFileStaticName()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));
            var bn = new BlobNameFactory("webhook/{yyyy}/{MM}/{dd}/logs-{yyyy}-{MM}-{dd}.txt");

            var result = bn.GetBlobName(dtoToApply);

            Assert.Equal("webhook/2018/11/05/logs-2018-11-05.txt", result);
        }
    }
}
