using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProjectAPI.Domain.Common.Interfaces;

namespace ProjectAPI.Api.Application.Common.BlobOperations.UploadToblob;


/// <summary>
/// Handler for uploading files to blob storage.
/// </summary>
public class UploadFilesHandler : IRequestHandler<UploadFilesCommand, UploadFilesResponse>
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UploadFilesHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadFilesHandler"/> class.
    /// </summary>
    /// <param name="blobStorageService">The blob storage service.</param>
    /// <param name="configuration">Application configuration, used to build public blob URLs.</param>
    /// <param name="logger">Logger.</param>
    public UploadFilesHandler(
        IBlobStorageService blobStorageService,
        IConfiguration configuration,
        ILogger<UploadFilesHandler> logger)
    {
        _blobStorageService = blobStorageService;
        _configuration = configuration;
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

        // Build the public URL from configuration rather than hard-coding the
        // account: the storage account can change without touching this handler.
        var accountUrl = (_configuration["BlobStorage:AccountUrl"] ?? string.Empty).TrimEnd('/');
        var containerName = _configuration["BlobStorage:ContainerName"] ?? "images";

        foreach (var file in request.Files)
        {
            if (file == null || file.Length == 0)
                continue;

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            using var stream = file.OpenReadStream();
            await _blobStorageService.UploadBlobAsync(fileName, stream, cancellationToken);

            fileLinks.Add($"{accountUrl}/{containerName}/{fileName}");
        }

        _logger.LogInformation("[UploadFiles] Uploaded {Count} file(s).", fileLinks.Count);

        return new UploadFilesResponse { FileLinks = fileLinks };
    }
}