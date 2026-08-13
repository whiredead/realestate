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

        public BlobContainer(BlobContainerClient client)
        {
            _client = client;
        }

        public async Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/octet-stream", CancellationToken cancellationToken = default)
        {
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
