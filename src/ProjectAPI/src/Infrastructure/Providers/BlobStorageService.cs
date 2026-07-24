using Azure.Storage.Blobs;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Infrastructure.Common;

namespace ProjectAPI.Infrastructure.Providers;

public class BlobStorageService : BaseBlobStorage, IBlobStorageService
{
    public BlobStorageService(BlobContainerClient containerClient) : base(containerClient)
    {
    }

    public async Task<bool> DeleteBlobAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    public async Task<(Stream? Content, string? ContentType, bool Exists)> DownloadBlobAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(fileName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return (null, null, false);
        }

        var response = await blobClient.DownloadAsync(cancellationToken);
        var contentType = response.Value.Details.ContentType ?? "application/octet-stream";

        return (response.Value.Content, contentType, true);
    }

    public async Task<string> UploadBlobAsync(string blobName, Stream content, CancellationToken cancellationToken = default)
    {
        return await UploadAsync(blobName, content, "application/octet-stream", cancellationToken);
    }
}
