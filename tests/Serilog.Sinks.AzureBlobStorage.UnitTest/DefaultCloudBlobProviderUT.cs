using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Specialized;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class DefaultCloudBlobProviderUT
    {
        private const string ContentType = "text/plain";
        private const int MaxBlocks = 50000;

        private readonly FakeBlobStorage storage = new FakeBlobStorage();
        private readonly DefaultCloudBlobProvider provider = new DefaultCloudBlobProvider();

        private Task<AppendBlobClient> GetBlobAsync(string blobName, long? blobSizeLimitBytes = null, bool bypass = true)
        {
            return provider.GetCloudBlobAsync(storage.ServiceClient, FakeBlobStorage.ContainerName, blobName, bypass, ContentType, blobSizeLimitBytes);
        }

        [Fact(DisplayName = "Should return same blob reference if name not changed and limits not reached")]
        public async Task ReturnSameBlobReferenceIfNameNotChangedAndLimitsNotReached()
        {
            AppendBlobClient first = await GetBlobAsync("SomeBlob.log", 2000);
            storage.Blobs["SomeBlob.log"].BlockCount = 1000;
            storage.Blobs["SomeBlob.log"].Length = 1000;

            AppendBlobClient second = await GetBlobAsync("SomeBlob.log", 2000);

            Assert.Same(first, second);
        }

        [Fact(DisplayName = "Should create the container only once")]
        public async Task CreateContainerOnlyOnce()
        {
            await GetBlobAsync("first.log");
            await GetBlobAsync("second.log");
            await GetBlobAsync("first.log");

            Assert.Equal(1, storage.ContainerCreateCalls);
        }

        [Fact(DisplayName = "Should not issue a create for a blob that already exists")]
        public async Task DoNotCreateExistingBlob()
        {
            storage.Add("SomeBlob.log", blockCount: 10);

            AppendBlobClient blob = await GetBlobAsync("SomeBlob.log");

            Assert.Equal("SomeBlob.log", blob.Name);
            Assert.Equal(0, storage.BlobCreateCalls);
        }

        [Fact(DisplayName = "Should return rolled blob reference if name not changed and max blocks reached")]
        public async Task ReturnRolledBlobReferenceIfNameNotChangedAndMaxBlocksReached()
        {
            AppendBlobClient first = await GetBlobAsync("SomeBlob.log");
            storage.Blobs["SomeBlob.log"].BlockCount = MaxBlocks;

            AppendBlobClient second = await GetBlobAsync("SomeBlob.log");

            Assert.Equal("SomeBlob.log", first.Name);
            Assert.Equal("SomeBlob-001.log", second.Name);
        }

        [Fact(DisplayName = "Should return rolled blob reference if name not changed and file size limit reached")]
        public async Task ReturnRolledBlobReferenceIfNameNotChangedAndFileSizeLimitReached()
        {
            AppendBlobClient first = await GetBlobAsync("SomeBlob.log", 2000);
            storage.Blobs["SomeBlob.log"].Length = 3000;

            AppendBlobClient second = await GetBlobAsync("SomeBlob.log", 2000);

            Assert.Equal("SomeBlob.log", first.Name);
            Assert.Equal("SomeBlob-001.log", second.Name);
        }

        [Fact(DisplayName = "Should apply the file size limit to the blobs that are considered when rolling")]
        public async Task ApplyFileSizeLimitWhenRolling()
        {
            await GetBlobAsync("SomeBlob.log", 2000);
            storage.Blobs["SomeBlob.log"].Length = 3000;
            storage.Add("SomeBlob-001.log", length: 3000);

            AppendBlobClient rolled = await GetBlobAsync("SomeBlob.log", 2000);

            Assert.Equal("SomeBlob-002.log", rolled.Name);
        }

        [Fact(DisplayName = "Should return rolled blob reference on init if limits reached")]
        public async Task ReturnRolledBlobReferenceOnInitIfLimitsReached()
        {
            storage.Add("SomeBlob.log", blockCount: MaxBlocks);
            storage.Add("SomeBlob-001.log", length: 3000);
            storage.Add("SomeBlob-002.log", blockCount: 10000, length: 1000);

            AppendBlobClient blob = await GetBlobAsync("SomeBlob.log", 2000);

            Assert.Equal("SomeBlob-002.log", blob.Name);
        }

        [Fact(DisplayName = "Should start from the unrolled name when the blob name changes")]
        public async Task ReturnNonRolledBlobReferenceIfNameChanged()
        {
            storage.Add("SomeBlob.log", blockCount: MaxBlocks);

            AppendBlobClient rolled = await GetBlobAsync("SomeBlob.log");
            AppendBlobClient other = await GetBlobAsync("SomeNewBlob.log");

            Assert.Equal("SomeBlob-001.log", rolled.Name);
            Assert.Equal("SomeNewBlob.log", other.Name);
        }

        [Fact(DisplayName = "Should keep rolled names inside their folder")]
        public async Task KeepRolledNamesInsideFolder()
        {
            storage.Add("webhook/2024/01.txt", blockCount: MaxBlocks);

            AppendBlobClient blob = await GetBlobAsync("webhook/2024/01.txt");

            Assert.Equal("webhook/2024/01-001.txt", blob.Name);
        }

        [Fact(DisplayName = "Should not validate again when alternating between known blobs")]
        public async Task TrackSeveralBlobNames()
        {
            await GetBlobAsync("Information.log");
            await GetBlobAsync("Error.log");
            int createCalls = storage.BlobCreateCalls;

            await GetBlobAsync("Information.log");
            await GetBlobAsync("Error.log");

            Assert.Equal(createCalls, storage.BlobCreateCalls);
            Assert.Equal(1, storage.ContainerCreateCalls);
        }

        [Fact(DisplayName = "Should recreate a blob that was deleted after it was first used")]
        public async Task RecreateDeletedBlob()
        {
            await GetBlobAsync("SomeBlob.log");
            storage.Blobs.Remove("SomeBlob.log");

            AppendBlobClient blob = await GetBlobAsync("SomeBlob.log");

            Assert.Equal("SomeBlob.log", blob.Name);
            Assert.True(storage.Blobs.ContainsKey("SomeBlob.log"));
        }

        [Fact(DisplayName = "Should keep writing to the last rolled blob when all roll sequences are used up")]
        public async Task ReturnLastRolledBlobWhenRollSequencesAreUsedUp()
        {
            storage.Add("SomeBlob.log", length: 3000);
            for (int i = 1; i <= 998; i++)
            {
                storage.Add($"SomeBlob-{i:D3}.log", length: 3000);
            }

            AppendBlobClient first = await GetBlobAsync("SomeBlob.log", 2000);
            AppendBlobClient second = await GetBlobAsync("SomeBlob.log", 2000);

            Assert.Equal("SomeBlob-998.log", first.Name);
            Assert.Same(first, second);
            Assert.Equal(999, storage.Blobs.Count);
        }

        [Fact(DisplayName = "Should throw exception if container cannot be created and not bypassed")]
        public async Task ThrowExceptionIfContainerCannotBeCreatedAndNoBypass()
        {
            storage.ContainerCreateException = new RequestFailedException(403, "Not authorized");

            await Assert.ThrowsAsync<RequestFailedException>(() => GetBlobAsync("SomeBlob.log", bypass: false));
        }

        [Fact(DisplayName = "Should not throw exception if container cannot be created and is bypassed")]
        public async Task DoNotThrowExceptionIfContainerCannotBeCreatedAndBypass()
        {
            storage.ContainerCreateException = new RequestFailedException(403, "Not authorized");

            AppendBlobClient blob = await GetBlobAsync("SomeBlob.log", bypass: true);
            await GetBlobAsync("OtherBlob.log", bypass: true);

            Assert.Equal("SomeBlob.log", blob.Name);
            Assert.Equal(1, storage.ContainerCreateCalls);
        }

        [Fact(DisplayName = "Should throw an exception if retainedBlobCountLimit is less than 1.")]
        public async Task DeleteArchivedBlobsAsync_PassLessThan1RetainedBlobCountLimit_ThrowsException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => provider.DeleteArchivedBlobsAsync(storage.ServiceClient, FakeBlobStorage.ContainerName, "SomeBlob.log", 0));
        }

        [Theory(DisplayName = "Should delete the oldest blobs (including rolled blobs) and retain the newest.")]
        [InlineData("SomeBlob-{yyyyMMdd}.log", new[] { "SomeBlob-20201201.log", "SomeBlob-20201202.log", "SomeBlob-20201203.log" })]
        [InlineData("SomeBlob-{yyyyMMdd}.log", new[] { "SomeBlob-20201201.log", "SomeBlob-20201202.log", "SomeBlob-20201202-001.log" })]
        [InlineData("{yyyy}/{dd}/{MM}/SomeBlobName.txt", new[] { "2020/01/12/SomeBlobName.txt", "2020/02/12/SomeBlobName.txt", "2020/03/12/SomeBlobName.txt" })]
        [InlineData("webhook/{yyyyMMdd}/{HH}.txt", new[] { "webhook/20201201/05.txt", "webhook/20201201/05-001.txt", "webhook/20201201/06.txt" })]
        [InlineData("log.txt", new[] { "log.txt", "log-001.txt", "log-002.txt" })]
        [InlineData("{Level}/log-{yyyy}.txt", new[] { "Error/log-2020.txt", "Information/log-2020.txt", "Error/log-2021.txt" })]
        public async Task DeleteArchivedBlobsAsync_DeletesOldestBlobs(string blobNameFormat, string[] blobNames)
        {
            foreach (string blobName in blobNames)
            {
                storage.Add(blobName);
            }

            await provider.DeleteArchivedBlobsAsync(storage.ServiceClient, FakeBlobStorage.ContainerName, blobNameFormat, 1);

            Assert.Equal(new[] { blobNames[2] }, storage.Blobs.Keys.ToArray());
            Assert.Equal(new[] { blobNames[1], blobNames[0] }, storage.DeletedBlobs.ToArray());
        }

        [Fact(DisplayName = "Should not delete anything when the limit is not exceeded")]
        public async Task DeleteArchivedBlobsAsync_LimitNotExceeded_DeletesNothing()
        {
            storage.Add("log-2020.txt");
            storage.Add("log-2021.txt");

            await provider.DeleteArchivedBlobsAsync(storage.ServiceClient, FakeBlobStorage.ContainerName, "log-{yyyy}.txt", 2);

            Assert.Empty(storage.DeletedBlobs);
        }

        [Fact(DisplayName = "Should only delete blobs whose whole name fits the name format")]
        public async Task DeleteArchivedBlobsAsync_LeavesOtherBlobsAlone()
        {
            string[] otherBlobs =
            {
                "catalog-2019.txt",
                "backup/log-2019.txt",
                "log-2019.txt.bak",
                "log-2019.txt/data.bin",
                "log-archive.txt"
            };

            foreach (string blobName in otherBlobs)
            {
                storage.Add(blobName);
            }

            storage.Add("log-2020.txt");
            storage.Add("log-2021.txt");

            await provider.DeleteArchivedBlobsAsync(storage.ServiceClient, FakeBlobStorage.ContainerName, "log-{yyyy}.txt", 1);

            Assert.Equal(new[] { "log-2020.txt" }, storage.DeletedBlobs.ToArray());
            Assert.All(otherBlobs, blobName => Assert.True(storage.Blobs.ContainsKey(blobName)));
        }

        [Fact(DisplayName = "Should list only the blobs that share the fixed start of the name format")]
        public async Task DeleteArchivedBlobsAsync_ListsByPrefix()
        {
            for (int i = 0; i < 5000; i++)
            {
                storage.Add($"data/{i}.bin");
            }

            storage.Add("logs/app-2020.txt");
            storage.Add("logs/app-2021.txt");

            await provider.DeleteArchivedBlobsAsync(storage.ServiceClient, FakeBlobStorage.ContainerName, "logs/app-{yyyy}.txt", 1);

            Assert.Equal("logs/app-", storage.LastListPrefix);
            Assert.Equal(2, storage.ListedBlobCount);
            Assert.Equal(new[] { "logs/app-2020.txt" }, storage.DeletedBlobs.ToArray());
        }

        [Theory(DisplayName = "Should derive the listing prefix from the fixed start of the name format")]
        [InlineData("log.txt", "log")]
        [InlineData("logs/log.txt", "logs/log")]
        [InlineData("logs/app-{yyyy}.txt", "logs/app-")]
        [InlineData("logs/{yyyy}/{MM}/log.txt", "logs/")]
        [InlineData("logs.v1/{yyyy}.txt", "logs.v1/")]
        [InlineData("app.{yyyy}", "app")]
        [InlineData("logfile", "logfile")]
        [InlineData("{yyyy}/log.txt", null)]
        public void GetListingPrefix(string blobNameFormat, string expectedPrefix)
        {
            Assert.Equal(expectedPrefix, DefaultCloudBlobProvider.GetListingPrefix(blobNameFormat));
        }
    }
}
