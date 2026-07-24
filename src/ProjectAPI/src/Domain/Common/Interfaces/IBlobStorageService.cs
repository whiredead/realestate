
using ProjectAPI.Domain.Common.Abstractions;

namespace ProjectAPI.Domain.Common.Interfaces;
public interface IBlobStorageService : IBaseBlobStorage
{
    Task<bool> DeleteBlobAsync(string fileName, CancellationToken cancellationToken);
    
    /// <summary>
    /// Downloads a blob from storage and returns its content as a stream.
    /// </summary>
    /// <param name="fileName">The name of the blob to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the stream, content type, and whether the blob exists.</returns>
    Task<(Stream? Content, string? ContentType, bool Exists)> DownloadBlobAsync(string fileName, CancellationToken cancellationToken);
    Task<string> UploadBlobAsync(string blobName, Stream content, CancellationToken cancellationToken = default);


}

