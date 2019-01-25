using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    public class DefaultCloudBlobProviderUT
    {
        private readonly CloudStorageAccount _storageAccount = A.Fake<CloudStorageAccount>(opt => opt.WithArgumentsForConstructor(new object[] { new StorageCredentials(), "account", "suffix.blobs.com", true }));
        private readonly CloudBlobClient _blobClient = A.Fake<CloudBlobClient>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com") }));

        private readonly string _blobContainerName = "logcontainer";
        private readonly CloudBlobContainer _blobContainer = A.Fake<CloudBlobContainer>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com/logcontainer") }));

        private readonly DefaultCloudBlobProvider _defaultCloudBlobProvider = new DefaultCloudBlobProvider();


        public DefaultCloudBlobProviderUT()
        {
            A.CallTo(() => _storageAccount.CreateCloudBlobClient()).Returns(_blobClient);
            A.CallTo(() => _blobClient.GetContainerReference(_blobContainerName)).Returns(_blobContainer);
            A.CallTo(() => _blobContainer.CreateIfNotExistsAsync()).Returns(Task.FromResult(true));
        }

        private CloudAppendBlob SetupCloudAppendBlobReference(string blobName, int blockCount)
        {
            CloudAppendBlob cloudAppendBlob = A.Fake<CloudAppendBlob>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com/logcontainer/" + blobName) }));

            SetCloudBlobBlockCount(cloudAppendBlob, blockCount);

            A.CallTo(() => cloudAppendBlob.Name).Returns(blobName);
            A.CallTo(() => cloudAppendBlob.CreateOrReplaceAsync(A<AccessCondition>.Ignored, null,null)).Returns(Task.FromResult(true));
            A.CallTo(() => cloudAppendBlob.FetchAttributesAsync()).Returns(Task.FromResult(true));

            A.CallTo(() => _blobContainer.GetAppendBlobReference(blobName)).Returns(cloudAppendBlob);

            return cloudAppendBlob;
        }

        private void SetCloudBlobBlockCount(CloudAppendBlob cloudAppendBlob, int newBlockCount)
        {
            cloudAppendBlob.Properties.GetType().GetProperty(nameof(BlobProperties.AppendBlobCommittedBlockCount)).SetValue(cloudAppendBlob.Properties, newBlockCount, null);
        }

        [Fact(DisplayName = "Should return same cloudblob if blobname is unchanged and max blocks has not been reached during.")]
        public async Task ReturnSameBlobReferenceIfNameNotChangedAndMaxBlocksNotReached()
        {
            string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 0);

            CloudAppendBlob firstRequest = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true);

            //Update blockcount to a value below the max block count
            SetCloudBlobBlockCount(firstRequest, 1000);

            CloudAppendBlob secondRequest = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true);

            Assert.Same(firstRequest, secondRequest);
        }


        [Fact(DisplayName = "Should return a rolled cloudblob if blobname is unchanged but max blocks has been reached during.")]
        public async Task ReturnRolledBlobReferenceIfNameNotChangedAndMaxBlocksReached()
        {
            string blobName = "SomeBlob.log";
            string rolledBlobName = "SomeBlob-001.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 40000);

            CloudAppendBlob firstRequest = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true);

            //Update blockcount to a value below the max block count
            SetCloudBlobBlockCount(firstRequest, 50000);

            //setup the rolled cloudblob
            CloudAppendBlob rolledCloudAppendBlob = SetupCloudAppendBlobReference(rolledBlobName, 0);

            CloudAppendBlob secondRequest = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true);

            Assert.NotSame(firstRequest, secondRequest);
            Assert.Equal(blobName, firstRequest.Name);
            Assert.Equal(rolledBlobName, secondRequest.Name);
        }

        [Fact(DisplayName = "Should return a rolled cloudblob on init if first blobs already reached the max block count.")]
        public async Task ReturnRolledBlobReferenceOnInitIfMaxBlocksReached()
        {
            string blobName = "SomeBlob.log";
            string firstRolledBlobName = "SomeBlob-001.log";
            string secondRolledBlobName = "SomeBlob-002.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 50000);
            CloudAppendBlob firstRolledCloudAppendBlob = SetupCloudAppendBlobReference(firstRolledBlobName, 50000);
            CloudAppendBlob secondRolledcloudAppendBlob = SetupCloudAppendBlobReference(secondRolledBlobName, 10000);

            CloudAppendBlob requestedBlob = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true);

            Assert.Equal(secondRolledBlobName, requestedBlob.Name);
        }

        [Fact(DisplayName = "Should return a new cloudblob non-rolled, if previous cloudblob was rolled.")]
        public async Task ReturnNonRolledBlobReferenceOnInitIfPreviousCloudblobWasRolled()
        {
            string blobName = "SomeBlob.log";
            string rolledBlobName = "SomeBlob-001.log";
            string newBlobName = "SomeNewBlob.log";

            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 50000);
            CloudAppendBlob firstRolledCloudAppendBlob = SetupCloudAppendBlobReference(rolledBlobName, 40000);
            CloudAppendBlob newCloudAppendBlob = SetupCloudAppendBlobReference(newBlobName, 0);

            CloudAppendBlob requestedBlob = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true);
            CloudAppendBlob requestednewBlob = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, newBlobName, true);

            Assert.Equal(rolledBlobName, requestedBlob.Name);
            Assert.Equal(newBlobName, requestednewBlob.Name);
        }

        [Fact(DisplayName = "Should throw exception if container cannot be created and bypass is false.")]
        public async Task ThrowExceptionIfContainerCannotBeCreatedAndNoBypass()
        {
            string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000);

            A.CallTo(() => _blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            await Assert.ThrowsAnyAsync<Exception>(() => _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, false));
        }

        [Fact(DisplayName = "Should not throw exception if container cannot be 'CreatedIfNotExists' and bypass is true.")]
        public async Task DoNoThrowExceptionIfContainerCannotBeCreatedAndBypass()
        {
            string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000);

            A.CallTo(() => _blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            CloudAppendBlob blob = await _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true);
        }

        [Fact(DisplayName = "Should throw exception if container cannot be 'CreatedIfNotExists' and bypass is true and container really does not exist.")]
        public async Task ThrowExceptionIfContainerCannotBeCreatedAndBypassAndContainerDoesNotExist()
        {

            A.CallTo(() => _blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000);
            A.CallTo(() => cloudAppendBlob.CreateOrReplaceAsync(A<AccessCondition>.Ignored, null, null)).Invokes(() => throw new StorageException());

            await Assert.ThrowsAnyAsync<Exception>(() => _defaultCloudBlobProvider.GetCloudBlobAsync(_storageAccount, _blobContainerName, blobName, true));
        }
    }
}
