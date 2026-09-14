using System.Collections.Concurrent;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Infrastructure.Settings;

namespace ProjectAPI.Infrastructure.Providers;

/// <summary>
/// One BlobServiceClient per connection (the account itself), built once from
/// BlobStorageSettings and cached for the process lifetime. Container clients
/// are cheap, stateless handles resolved on demand from that single
/// connection, so adding/using a new container is just passing its name to
/// ForContainer — no new registration, no restart.
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly string _defaultContainerName;
    private readonly ConcurrentDictionary<string, BlobContainer> _containers = new();

    public BlobStorageService(BlobServiceClient serviceClient, BlobStorageSettings settings)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        _defaultContainerName = settings.ContainerName;
    }

    public IBlobContainer ForContainer(string? containerName = null)
    {
        var name = string.IsNullOrWhiteSpace(containerName) ? _defaultContainerName : containerName;
        return _containers.GetOrAdd(name, n => new BlobContainer(_serviceClient.GetBlobContainerClient(n)));
    }

    private sealed class BlobContainer : IBlobContainer
    {
        private readonly BlobContainerClient _client;

        /// <summary>
        /// One existence check per container per process. Container clients are
        /// cached for the app's lifetime (see the dictionary above), so this is
        /// a single extra call on the first upload, not one per file.
        /// </summary>
        private volatile bool _ensured;

        public BlobContainer(BlobContainerClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Creates the container on first use, PRIVATE.
        ///
        /// Verified against the live account: "documents" did not exist at all,
        /// so every reservation-document upload failed with ContainerNotFound —
        /// nothing here had ever created it and it had to be made by hand. The
        /// access level is the reason this is not left to whoever does that by
        /// hand: "images" was created with public blob access (correct — the
        /// public catalogue renders those), and a container of CIN scans and
        /// signed contracts created the same way would be readable by anyone
        /// holding a URL.
        ///
        /// PublicAccessType.None is only applied when the container is created.
        /// An existing container keeps the access level it has, so this never
        /// silently changes "images" underneath the public site.
        /// </summary>
        private async Task EnsurePrivateContainerAsync(CancellationToken cancellationToken)
        {
            if (_ensured) return;

            await _client.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            _ensured = true;
        }

        public async Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/octet-stream", CancellationToken cancellationToken = default)
        {
            await EnsurePrivateContainerAsync(cancellationToken);

            var blobClient = _client.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
            return blobClient.Uri.ToString();
        }

        public async Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default)
        {
            var response = await _client.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return response.Value;
        }

        public async Task<(Stream? Content, string? ContentType, bool Exists)> DownloadAsync(string blobName, CancellationToken cancellationToken = default)
        {
            var blobClient = _client.GetBlobClient(blobName);
            if (!await blobClient.ExistsAsync(cancellationToken))
                return (null, null, false);

            var response = await blobClient.DownloadAsync(cancellationToken);
            var contentType = response.Value.Details.ContentType ?? "application/octet-stream";
            return (response.Value.Content, contentType, true);
        }

        public string GetUrl(string blobName) => _client.GetBlobClient(blobName).Uri.ToString();
    }
}
