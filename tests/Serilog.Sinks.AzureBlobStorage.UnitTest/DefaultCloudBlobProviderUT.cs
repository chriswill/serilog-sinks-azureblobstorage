﻿using System;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Serilog.Sinks.AzureBlobStorage.AzureBlobProvider;
using Xunit;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    /// <summary>
    /// These tests need updates.  In v10 of the Windows Azure Storage libraries, CreateCloudBlobClient() is now a static extension method, so it can no longer be mocked.
    /// </summary>
    /// 
    public class DefaultCloudBlobProviderUT
    {      
        private readonly CloudBlobClient blobClient = A.Fake<CloudBlobClient>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com"), new StorageCredentials(), null }));        

        private readonly string blobContainerName = "logcontainer";
        private readonly CloudBlobContainer blobContainer = A.Fake<CloudBlobContainer>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com/logcontainer") }));

        private readonly DefaultCloudBlobProvider defaultCloudBlobProvider = new DefaultCloudBlobProvider();
        
        public DefaultCloudBlobProviderUT()
        {
            A.CallTo(() => blobClient.GetContainerReference(blobContainerName)).Returns(blobContainer);            
            A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Returns(Task.FromResult(true));
        }

        private CloudAppendBlob SetupCloudAppendBlobReference(string blobName, int blockCount, int filesize)
        {
            CloudAppendBlob cloudAppendBlob = A.Fake<CloudAppendBlob>(opt => opt.WithArgumentsForConstructor(new object[] { new Uri("https://account.suffix.blobs.com/logcontainer/" + blobName) }));

            SetCloudBlobBlockCount(cloudAppendBlob, blockCount);
            SetBlobLength(cloudAppendBlob, filesize);

            A.CallTo(() => cloudAppendBlob.Name).Returns(blobName);
            A.CallTo(() => cloudAppendBlob.CreateOrReplaceAsync(A<AccessCondition>.Ignored, null,null)).Returns(Task.FromResult(true));
            A.CallTo(() => cloudAppendBlob.FetchAttributesAsync()).Returns(Task.FromResult(true));

            A.CallTo(() => blobContainer.GetAppendBlobReference(blobName)).Returns(cloudAppendBlob);

            return cloudAppendBlob;
        }

        private void SetCloudBlobBlockCount(CloudAppendBlob cloudAppendBlob, int newBlockCount)
        {
            cloudAppendBlob.Properties.GetType().GetProperty(nameof(BlobProperties.AppendBlobCommittedBlockCount)).SetValue(cloudAppendBlob.Properties, newBlockCount, null);
        }

        private void SetBlobLength(CloudAppendBlob cloudAppendBlob, int newLength)
        {
            cloudAppendBlob.Properties.GetType().GetProperty(nameof(BlobProperties.Length)).SetValue(cloudAppendBlob.Properties, newLength, null);
        }

        [Fact(DisplayName = "Should return same blob reference if name not changed and max blocks not reached")]
        public async Task ReturnSameBlobReferenceIfNameNotChangedAndMaxBlocksNotReached()
        {
            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 0, 0);

            CloudAppendBlob firstRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true);

            //Update blockcount to a value below the max block count
            SetCloudBlobBlockCount(firstRequest, 1000);

            CloudAppendBlob secondRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true);

            Assert.Same(firstRequest, secondRequest);
        }

        [Fact(DisplayName = "Should return same blob reference if name not changed and file size limit not reached")]
        public async Task ReturnSameBlobReferenceIfNameNotChangedAndFileSizeLimitNotReached()
        {
            const string blobName = "SomeBlob.log";
            const long fileSizeLimitBytes = 2000;
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 0, 0);

            CloudAppendBlob firstRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true, fileSizeLimitBytes);

            //Update file size to a value below the file size limit
            SetBlobLength(firstRequest, 1000);

            CloudAppendBlob secondRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true, fileSizeLimitBytes);

            Assert.Same(firstRequest, secondRequest);
        }

        [Fact(DisplayName = "Should return rolled blob reference if name not changed and max blocks reached")]
        public async Task ReturnRolledBlobReferenceIfNameNotChangedAndMaxBlocksReached()
        {
            const string blobName = "SomeBlob.log";
            const string rolledBlobName = "SomeBlob-001.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 40000, 0);

            CloudAppendBlob firstRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true);

            //Update blockcount to a value below the max block count
            SetCloudBlobBlockCount(firstRequest, 50000);

            //setup the rolled cloudblob
            CloudAppendBlob rolledCloudAppendBlob = SetupCloudAppendBlobReference(rolledBlobName, 0, 0);

            CloudAppendBlob secondRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true);

            Assert.NotSame(firstRequest, secondRequest);
            Assert.Equal(blobName, firstRequest.Name);
            Assert.Equal(rolledBlobName, secondRequest.Name);
        }

        [Fact(DisplayName = "Should return rolled blob reference if name not changed and file size limit reached")]
        public async Task ReturnRolledBlobReferenceIfNameNotChangedAndFileSizeLimitReached()
        {
            const string blobName = "SomeBlob.log";
            const string rolledBlobName = "SomeBlob-001.log";
            const long fileSizeLimitBytes = 2000;
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 0, 0);
            
            CloudAppendBlob firstRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true, fileSizeLimitBytes);

            SetBlobLength(cloudAppendBlob, 3000);

            //setup the rolled cloudblob
            CloudAppendBlob rolledCloudAppendBlob = SetupCloudAppendBlobReference(rolledBlobName, 0, 0);

            CloudAppendBlob secondRequest = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true, fileSizeLimitBytes);

            Assert.NotSame(firstRequest, secondRequest);
            Assert.Equal(blobName, firstRequest.Name);
            Assert.Equal(rolledBlobName, secondRequest.Name);
        }

        [Fact(DisplayName = "Should return rolled blob reference on init if max blocks reached")]
        public async Task ReturnRolledBlobReferenceOnInitIfMaxBlocksReached()
        {
            const string blobName = "SomeBlob.log";
            const string firstRolledBlobName = "SomeBlob-001.log";
            const string secondRolledBlobName = "SomeBlob-002.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 50000, 0);
            CloudAppendBlob firstRolledCloudAppendBlob = SetupCloudAppendBlobReference(firstRolledBlobName, 50000, 0);
            CloudAppendBlob secondRolledcloudAppendBlob = SetupCloudAppendBlobReference(secondRolledBlobName, 10000, 0);

            CloudAppendBlob requestedBlob = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true);

            Assert.Equal(secondRolledBlobName, requestedBlob.Name);
        }

        [Fact(DisplayName = "Should return rolled blob reference on init if file size limit reached")]
        public async Task ReturnRolledBlobReferenceOnInitIfFileSizeLimitReached()
        {
            const string blobName = "SomeBlob.log";
            const string firstRolledBlobName = "SomeBlob-001.log";
            const string secondRolledBlobName = "SomeBlob-002.log";
            const long fileSizeLimitBytes = 2000;
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 0, 3000);
            CloudAppendBlob firstRolledCloudAppendBlob = SetupCloudAppendBlobReference(firstRolledBlobName, 0, 3000);
            CloudAppendBlob secondRolledcloudAppendBlob = SetupCloudAppendBlobReference(secondRolledBlobName, 0, 1000);

            CloudAppendBlob requestedBlob = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true, fileSizeLimitBytes);

            Assert.Equal(secondRolledBlobName, requestedBlob.Name);
        }

        [Fact(DisplayName = "Should return rolled blob reference on init if previous cloud blob was rolled")]
        public async Task ReturnNonRolledBlobReferenceOnInitIfPreviousCloudBlobWasRolled()
        {
            const string blobName = "SomeBlob.log";
            const string rolledBlobName = "SomeBlob-001.log";
            const string newBlobName = "SomeNewBlob.log";

            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 50000, 0);
            CloudAppendBlob firstRolledCloudAppendBlob = SetupCloudAppendBlobReference(rolledBlobName, 40000, 0);
            CloudAppendBlob newCloudAppendBlob = SetupCloudAppendBlobReference(newBlobName, 0, 0);

            CloudAppendBlob requestedBlob = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true);
            CloudAppendBlob requestednewBlob = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, newBlobName, true);

            Assert.Equal(rolledBlobName, requestedBlob.Name);
            Assert.Equal(newBlobName, requestednewBlob.Name);
        }

        [Fact(DisplayName = "Should throw exception if container cannot be created and not bypassed")]
        public async Task ThrowExceptionIfContainerCannotBeCreatedAndNoBypass()
        {
            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000, 0);

            A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            await Assert.ThrowsAnyAsync<Exception>(() => defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, false));
        }

        [Fact(DisplayName = "Should not throw exception if container cannot be created and is bypassed")]
        public async Task DoNoThrowExceptionIfContainerCannotBeCreatedAndBypass()
        {
            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000, 0);

            A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            CloudAppendBlob blob = await defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true);
        }

        [Fact(DisplayName = "Should throw exception if container cannot be created and is bypassed and container does not exist")]
        public async Task ThrowExceptionIfContainerCannotBeCreatedAndBypassAndContainerDoesNotExist()
        {
            A.CallTo(() => blobContainer.CreateIfNotExistsAsync()).Invokes(() => throw new StorageException());

            const string blobName = "SomeBlob.log";
            CloudAppendBlob cloudAppendBlob = SetupCloudAppendBlobReference(blobName, 1000, 0);
            A.CallTo(() => cloudAppendBlob.CreateOrReplaceAsync(A<AccessCondition>.Ignored, null, null)).Invokes(() => throw new StorageException());

            await Assert.ThrowsAnyAsync<Exception>(() => defaultCloudBlobProvider.GetCloudBlobAsync(blobClient, blobContainerName, blobName, true));
        }
    }
}
