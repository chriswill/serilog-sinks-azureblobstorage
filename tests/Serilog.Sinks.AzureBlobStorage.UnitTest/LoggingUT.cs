using System;
using Azure.Storage;
using Azure.Storage.Blobs;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class LoggingUT
    {
        private const string ConnectionString = "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";
        private readonly CloudBlobClient blobClient;

        public LoggingUT()
        {
            //  TODO-VPL:  No idea how to use dev storage in the new SDK ; disable complilation of the file
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            blobClient = cloudStorageAccount.CreateCloudBlobClient();
        }

        [Fact(DisplayName = "Should throw validation exception due to invalid format characters.")]
        public void InvalidFormatCharacters()
        {
            var log = new LoggerConfiguration()
                .WriteTo.AzureBlobStorage(ConnectionString)
                .CreateLogger();
        }

        [Fact(DisplayName = "Should throw validation exception due to format characters not accepted")]
        public void OutOfOrderFormatCharacters()
        {
            var dtoToApply = new DateTimeOffset(2018, 11, 5, 8, 30, 0, new TimeSpan(-5, 0, 0));

            Assert.Throws<ArgumentException>(() => new BlobNameFactory(@"{xx}\name.txt"));
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
