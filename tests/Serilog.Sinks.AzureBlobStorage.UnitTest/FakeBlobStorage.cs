using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using FakeItEasy;

namespace Serilog.Sinks.AzureBlobStorage.UnitTest
{
    /// <summary>
    /// An in-memory stand-in for a blob container, built from fakes of the Azure SDK clients.
    /// </summary>
    internal class FakeBlobStorage
    {
        public const string ContainerName = "logcontainer";

        public class FakeBlob
        {
            public int BlockCount;
            public long Length;
            public DateTimeOffset CreatedOn;
        }

        private readonly Dictionary<string, AppendBlobClient> clients = new Dictionary<string, AppendBlobClient>();
        private DateTimeOffset clock = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public BlobServiceClient ServiceClient { get; } = A.Fake<BlobServiceClient>();
        public BlobContainerClient ContainerClient { get; } = A.Fake<BlobContainerClient>();

        public Dictionary<string, FakeBlob> Blobs { get; } = new Dictionary<string, FakeBlob>();

        public int ContainerCreateCalls { get; private set; }
        public int BlobCreateCalls { get; private set; }
        public int GetPropertiesCalls { get; private set; }
        public int ListCalls { get; private set; }
        public int ListedBlobCount { get; private set; }
        public string LastListPrefix { get; private set; }
        public List<string> DeletedBlobs { get; } = new List<string>();

        public Exception ContainerCreateException { get; set; }

        public FakeBlobStorage()
        {
            A.CallTo(() => ServiceClient.GetBlobContainerClient(ContainerName)).Returns(ContainerClient);

            A.CallTo(ContainerClient)
                .Where(call => call.Method.Name == "CreateIfNotExistsAsync")
                .WithReturnType<Task<Response<BlobContainerInfo>>>()
                .ReturnsLazily(call =>
                {
                    ContainerCreateCalls++;
                    if (ContainerCreateException != null)
                    {
                        throw ContainerCreateException;
                    }

                    return Task.FromResult<Response<BlobContainerInfo>>(null);
                });

            A.CallTo(ContainerClient)
                .Where(call => call.Method.Name == "GetAppendBlobClientCore")
                .WithReturnType<AppendBlobClient>()
                .ReturnsLazily(call => GetClient((string)call.Arguments[0]));

            A.CallTo(() => ContainerClient.GetBlobsAsync(A<BlobTraits>._, A<BlobStates>._, A<string>._, A<CancellationToken>._))
                .ReturnsLazily((BlobTraits traits, BlobStates states, string prefix, CancellationToken cancellationToken) => List(prefix));
        }

        public FakeBlob Add(string name, int blockCount = 0, long length = 0)
        {
            clock = clock.AddMinutes(1);
            FakeBlob blob = new FakeBlob { BlockCount = blockCount, Length = length, CreatedOn = clock };
            Blobs[name] = blob;
            return blob;
        }

        private AsyncPageable<BlobItem> List(string prefix)
        {
            ListCalls++;
            LastListPrefix = prefix;

            List<BlobItem> items = Blobs
                .Where(blob => prefix == null || blob.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(blob => BlobsModelFactory.BlobItem(
                    name: blob.Key,
                    properties: BlobsModelFactory.BlobItemProperties(accessTierInferred: false, createdOn: blob.Value.CreatedOn)))
                .ToList();

            ListedBlobCount += items.Count;

            return AsyncPageable<BlobItem>.FromPages(new[] { Page<BlobItem>.FromValues(items, null, A.Fake<Response>()) });
        }

        private AppendBlobClient GetClient(string name)
        {
            if (clients.TryGetValue(name, out AppendBlobClient existing))
            {
                return existing;
            }

            AppendBlobClient client = A.Fake<AppendBlobClient>();
            A.CallTo(() => client.Name).Returns(name);

            A.CallTo(() => client.GetPropertiesAsync(A<BlobRequestConditions>._, A<CancellationToken>._))
                .ReturnsLazily(call =>
                {
                    GetPropertiesCalls++;
                    if (!Blobs.TryGetValue(name, out FakeBlob blob))
                    {
                        throw new RequestFailedException(404, "The specified blob does not exist.", "BlobNotFound", null);
                    }

                    BlobProperties properties = BlobsModelFactory.BlobProperties(contentLength: blob.Length, blobCommittedBlockCount: blob.BlockCount);
                    return Task.FromResult(Response.FromValue(properties, A.Fake<Response>()));
                });

            A.CallTo(() => client.CreateIfNotExistsAsync(A<AppendBlobCreateOptions>._, A<CancellationToken>._))
                .ReturnsLazily(call =>
                {
                    BlobCreateCalls++;
                    if (Blobs.ContainsKey(name))
                    {
                        return Task.FromResult<Response<BlobContentInfo>>(null);
                    }

                    Add(name);
                    BlobContentInfo info = BlobsModelFactory.BlobContentInfo(default, default, default, default, default, default, default);
                    return Task.FromResult(Response.FromValue(info, A.Fake<Response>()));
                });

            A.CallTo(() => client.DeleteIfExistsAsync(A<DeleteSnapshotsOption>._, A<BlobRequestConditions>._, A<CancellationToken>._))
                .ReturnsLazily(call =>
                {
                    DeletedBlobs.Add(name);
                    return Task.FromResult(Response.FromValue(Blobs.Remove(name), A.Fake<Response>()));
                });

            clients[name] = client;
            return client;
        }
    }
}
