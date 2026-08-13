using Microsoft.Extensions.Logging;
using ProjectAPI.Domain.Common.Interfaces;

namespace ProjectAPI.Api.Application.Common.BlobOperations.UploadToblob;


/// <summary>
/// Handler for uploading files to blob storage (default container — see
/// BlobStorageSettings.ContainerName; callers needing a different container
/// go through IBlobStorageService.ForContainer directly rather than this
/// generic endpoint).
/// </summary>
public class UploadFilesHandler : IRequestHandler<UploadFilesCommand, UploadFilesResponse>
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<UploadFilesHandler> _logger;

    public UploadFilesHandler(IBlobStorageService blobStorageService, ILogger<UploadFilesHandler> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the file upload command.
    /// </summary>
    /// <param name="request">The upload file command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response containing the list of file links.</returns>
    public async Task<UploadFilesResponse> Handle(UploadFilesCommand request, CancellationToken cancellationToken)
    {
        var fileLinks = new List<string>();
        var container = _blobStorageService.ForContainer();

        foreach (var file in request.Files)
        {
            if (file == null || file.Length == 0)
                continue;

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            using var stream = file.OpenReadStream();
            var url = await container.UploadAsync(fileName, stream, file.ContentType ?? "application/octet-stream", cancellationToken);

            fileLinks.Add(url);
        }

        _logger.LogInformation("[UploadFiles] Uploaded {Count} file(s).", fileLinks.Count);

        return new UploadFilesResponse { FileLinks = fileLinks };
    }
}