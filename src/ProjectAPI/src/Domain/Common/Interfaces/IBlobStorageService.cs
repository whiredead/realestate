namespace ProjectAPI.Domain.Common.Interfaces;

/// <summary>
/// Entry point for blob storage. One <see cref="BlobStorageSettings"/>-bound
/// connection backs every container; <see cref="ForContainer"/> resolves the
/// container to operate on per call, so a caller can target "images",
/// "documents", or any other container on demand instead of the service
/// being locked to a single container for the app's lifetime.
/// </summary>
public interface IBlobStorageService
{
    /// <param name="containerName">
    /// Container to operate on. Null/omitted uses BlobStorageSettings.ContainerName
    /// (the existing default — callers that never named a container keep working
    /// unchanged).
    /// </param>
    IBlobContainer ForContainer(string? containerName = null);
}

/// <summary>Blob operations scoped to one container, resolved via <see cref="IBlobStorageService.ForContainer"/>.</summary>
public interface IBlobContainer
{
    Task<string> UploadAsync(string blobName, Stream content, string contentType = "application/octet-stream", CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default);
    Task<(Stream? Content, string? ContentType, bool Exists)> DownloadAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>Public URL for a blob in this container, without a round trip — matches what UploadAsync returns.</summary>
    string GetUrl(string blobName);
}
