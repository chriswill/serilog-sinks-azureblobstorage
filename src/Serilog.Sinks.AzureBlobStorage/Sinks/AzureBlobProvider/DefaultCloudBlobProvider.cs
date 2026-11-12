﻿// Copyright 2018 CloudScope, LLC
// Portions copyright 2014 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Serilog.Sinks.AzureBlobStorage.AzureBlobProvider
{
    public class DefaultCloudBlobProvider : ICloudBlobProvider
    {
        private const int MaxBlocksOnBlobBeforeRoll = 49500; //small margin to the practical max of 50k, in case of many multiple writers to the same blob

        // Rolled blobs are named <name>-001 through <name>-998.
        private const int MaxRollSequence = 998;

        // Upper bound on the number of blob names tracked at once. More than one is tracked so that
        // name templates using {Level} or custom properties do not re-validate the blob on every switch.
        private const int MaxTrackedBlobs = 64;

        private sealed class BlobState
        {
            public AppendBlobClient Client;
            public int RollSequence;
            public long LastUsed;
        }

        private readonly object syncRoot = new object();
        private readonly Dictionary<string, BlobState> blobStates = new Dictionary<string, BlobState>(StringComparer.Ordinal);
        private readonly HashSet<string> ensuredContainers = new HashSet<string>(StringComparer.Ordinal);
        private long useCounter;

        private string archiveRegexFormat;
        private Regex archiveRegex;

        public async Task<AppendBlobClient> GetCloudBlobAsync(BlobServiceClient blobServiceClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation, string contentType, long? blobSizeLimitBytes = null)
        {
            string stateKey = blobContainerName + "/" + blobName;
            BlobState state = GetState(stateKey);
            int startSequence = 0;
            AppendBlobClient fallback = null;

            if (state != null)
            {
                // The blob is already known; fetch its attributes to check the block count and size limits.
                try
                {
                    Response<BlobProperties> propertiesResponse = await state.Client.GetPropertiesAsync().ConfigureAwait(false);

                    if (ValidateBlobProperties(propertiesResponse.Value, blobSizeLimitBytes))
                    {
                        return state.Client;
                    }

                    // The blob is correct but needs to be rolled over
                    startSequence = state.RollSequence + 1;
                    fallback = state.Client;
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    // The blob was removed since it was last written to; start over.
                    RemoveState(stateKey);
                }
            }

            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(blobContainerName);
            await EnsureContainerAsync(blobContainer, blobContainerName, bypassBlobCreationValidation).ConfigureAwait(false);

            //try to get a reference to an AppendBlobClient which is below the max blocks threshold.
            for (int i = startSequence; i <= MaxRollSequence; i++)
            {
                AppendBlobClient candidate = blobContainer.GetAppendBlobClient(GetRolledBlobName(blobName, i));
                BlobProperties properties = await GetOrCreateBlobAsync(candidate, blobContainerName, contentType).ConfigureAwait(false);

                // null properties means that the blob was just created, and so is empty
                if (properties == null || ValidateBlobProperties(properties, blobSizeLimitBytes))
                {
                    SetState(stateKey, candidate, i);
                    return candidate;
                }

                fallback = candidate;
            }

            // Every roll sequence is used up. Keep writing to the last blob rather than losing events.
            Debugging.SelfLog.WriteLine("All {0} rolled blobs for '{1}' have reached their limits; continuing to write to the last one.", MaxRollSequence, blobName);
            SetState(stateKey, fallback, MaxRollSequence);
            return fallback;
        }

        private BlobState GetState(string stateKey)
        {
            lock (syncRoot)
            {
                if (blobStates.TryGetValue(stateKey, out BlobState state))
                {
                    state.LastUsed = ++useCounter;
                    return state;
                }

                return null;
            }
        }

        private void SetState(string stateKey, AppendBlobClient client, int rollSequence)
        {
            lock (syncRoot)
            {
                if (!blobStates.ContainsKey(stateKey) && blobStates.Count >= MaxTrackedBlobs)
                {
                    string leastRecentlyUsed = blobStates.OrderBy(pair => pair.Value.LastUsed).First().Key;
                    blobStates.Remove(leastRecentlyUsed);
                }

                blobStates[stateKey] = new BlobState { Client = client, RollSequence = rollSequence, LastUsed = ++useCounter };
            }
        }

        private void RemoveState(string stateKey)
        {
            lock (syncRoot)
            {
                blobStates.Remove(stateKey);
            }
        }

        private static bool ValidateBlobProperties(BlobProperties properties, long? blobSizeLimitBytes)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            return properties.BlobCommittedBlockCount < MaxBlocksOnBlobBeforeRoll
                && (blobSizeLimitBytes == null || properties.ContentLength < blobSizeLimitBytes);
        }

        private static string GetRolledBlobName(string blobName, int rollingSequenceNumber)
        {
            //On first try just return the unchanged blobname
            if (rollingSequenceNumber == 0)
            {
                return blobName;
            }

            //append the sequence number to the filename
            string newFileName = $"{Path.GetFileNameWithoutExtension(blobName)}-{rollingSequenceNumber:D3}{Path.GetExtension(blobName)}";
            return Path.Combine(Path.GetDirectoryName(blobName), newFileName).Replace('\\', '/');
        }

        /// <summary>
        /// Returns the properties of the blob, creating the blob when it does not exist yet.
        /// Reading first avoids a (billed) write operation for every blob that already exists.
        /// </summary>
        /// <returns>The blob properties, or null when the blob was just created.</returns>
        private async Task<BlobProperties> GetOrCreateBlobAsync(AppendBlobClient appendBlobClient, string blobContainerName, string contentType)
        {
            try
            {
                Response<BlobProperties> existing = await appendBlobClient.GetPropertiesAsync().ConfigureAwait(false);
                return existing.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // The blob does not exist yet; create it below.
            }

            Response<BlobContentInfo> created = await CreateBlobIfNotExistsAsync(appendBlobClient, blobContainerName, contentType).ConfigureAwait(false);
            if (created != null)
            {
                return null;
            }

            // Another writer created the blob in the meantime.
            Response<BlobProperties> properties = await appendBlobClient.GetPropertiesAsync().ConfigureAwait(false);
            return properties.Value;
        }

        private async Task<Response<BlobContentInfo>> CreateBlobIfNotExistsAsync(AppendBlobClient appendBlobClient, string blobContainerName, string contentType)
        {
            try
            {
                return await appendBlobClient.CreateIfNotExistsAsync(
                    new AppendBlobCreateOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType
                        }
                    }).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict && ex.ErrorCode == "BlobAlreadyExists")
            {
                //StorageException (http 409 conflict, error code BlobAlreadyExists) is thrown due to the AccessCondition. The append blob already exists.
                //No problem this is expected
                return null;
            }
            catch (Exception ex)
            {
                if (ex is RequestFailedException requestFailed && requestFailed.ErrorCode == "ContainerNotFound")
                {
                    // The container went away; check for it again on the next attempt.
                    lock (syncRoot)
                    {
                        ensuredContainers.Remove(blobContainerName);
                    }
                }

                Debugging.SelfLog.WriteLine("Failed to create blob: {0}", ex);
                throw;
            }
        }

        public async Task<AppendBlobClient> GetBlobReferenceAsync(BlobServiceClient blobServiceClient, string blobContainerName, string blobName, bool bypassBlobCreationValidation, string contentType)
        {
            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(blobContainerName);

            await EnsureContainerAsync(blobContainer, blobContainerName, bypassBlobCreationValidation).ConfigureAwait(false);

            AppendBlobClient newAppendBlobClient = blobContainer.GetAppendBlobClient(blobName);
            await CreateBlobIfNotExistsAsync(newAppendBlobClient, blobContainerName, contentType).ConfigureAwait(false);

            return newAppendBlobClient;
        }

        /// <summary>
        /// Creates the container if required. This is done once per container, not for every blob.
        /// </summary>
        private async Task EnsureContainerAsync(BlobContainerClient blobContainerClient, string blobContainerName, bool bypassBlobCreationValidation)
        {
            lock (syncRoot)
            {
                if (ensuredContainers.Contains(blobContainerName))
                {
                    return;
                }
            }

            try
            {
                await blobContainerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debugging.SelfLog.WriteLine("Failed to create blob container: {0}", ex);
                if (!bypassBlobCreationValidation)
                {
                    throw;
                }
            }

            lock (syncRoot)
            {
                ensuredContainers.Add(blobContainerName);
            }
        }

        public async Task DeleteArchivedBlobsAsync(BlobServiceClient blobServiceClient, string blobContainerName, string blobNameFormat, int retainedBlobCountLimit)
        {
            if (retainedBlobCountLimit < 1)
            {
                throw new ArgumentException("Invalid value provided; retained blob count limit must be at least 1 or null.");
            }

            BlobContainerClient blobContainer = blobServiceClient.GetBlobContainerClient(blobContainerName);
            Regex regex = GetArchiveRegex(blobNameFormat);

            // Only the name and creation time of blobs written by this sink are kept, so that memory use
            // does not grow with the number of other blobs in the container.
            List<KeyValuePair<string, DateTimeOffset>> logBlobs = new List<KeyValuePair<string, DateTimeOffset>>();

            // Let the service filter on the fixed leading part of the name, which keeps unrelated blobs out of the listing.
            AsyncPageable<BlobItem> blobItems = blobContainer.GetBlobsAsync(prefix: GetListingPrefix(blobNameFormat));

            IAsyncEnumerator<BlobItem> enumerator = blobItems.GetAsyncEnumerator();
            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    BlobItem blobItem = enumerator.Current;
                    if (regex.IsMatch(RemoveRolledBlobNameSerialNum(blobItem.Name)))
                    {
                        logBlobs.Add(new KeyValuePair<string, DateTimeOffset>(blobItem.Name, blobItem.Properties?.CreatedOn ?? DateTimeOffset.MinValue));
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            if (logBlobs.Count <= retainedBlobCountLimit)
            {
                return;
            }

            IEnumerable<string> blobsToDelete = logBlobs
                .OrderByDescending(blob => blob.Value)
                .Skip(retainedBlobCountLimit)
                .Select(blob => blob.Key);

            foreach (string blobName in blobsToDelete)
            {
                try
                {
                    await blobContainer.GetAppendBlobClient(blobName).DeleteIfExistsAsync().ConfigureAwait(false);
                }
                catch (RequestFailedException ex)
                {
                    Debugging.SelfLog.WriteLine("Failed to delete archived blob '{0}': {1}", blobName, ex);
                }
            }
        }

        private Regex GetArchiveRegex(string blobNameFormat)
        {
            lock (syncRoot)
            {
                if (archiveRegex == null || !string.Equals(archiveRegexFormat, blobNameFormat, StringComparison.Ordinal))
                {
                    // Anchored, so that only blobs whose whole name fits the format are candidates for deletion.
                    string pattern = new BlobNameFactory(blobNameFormat).GetBlobRegex();
                    archiveRegex = new Regex("^(?:" + pattern + ")$", RegexOptions.CultureInvariant);
                    archiveRegexFormat = blobNameFormat;
                }

                return archiveRegex;
            }
        }

        /// <summary>
        /// The fixed text that every blob written for the given name format starts with, or null if there is none.
        /// </summary>
        internal static string GetListingPrefix(string blobNameFormat)
        {
            int firstToken = blobNameFormat.IndexOf('{');
            string prefix = firstToken < 0 ? blobNameFormat : blobNameFormat.Substring(0, firstToken);

            // Rolled blobs have their sequence number inserted before the extension, so stop ahead of it.
            int extension = prefix.LastIndexOf('.');
            if (extension > prefix.LastIndexOf('/'))
            {
                prefix = prefix.Substring(0, extension);
            }

            return prefix.Length == 0 ? null : prefix;
        }

        private static string RemoveRolledBlobNameSerialNum(string blobName)
        {
            string blobNameWoExtension = Path.ChangeExtension(blobName, null);
            blobNameWoExtension = Regex.Replace(blobNameWoExtension, "-[0-9]{3}$", string.Empty);
            return blobNameWoExtension + Path.GetExtension(blobName);
        }

    }
}
