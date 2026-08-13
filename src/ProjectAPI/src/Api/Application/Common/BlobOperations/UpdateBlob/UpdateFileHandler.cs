using ProjectAPI.Domain.Common.Interfaces;

namespace ProjectAPI.Api.Application.Common.BlobOperations.UpdateBlob;

/// <summary>
/// Handler for updating a file in blob storage.
/// </summary>
public class UpdateFileHandler : IRequestHandler<UpdateFileCommand, UpdateFileResponse>
{
    private readonly IBlobStorageService _blobStorageService;

    public UpdateFileHandler(IBlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    public async Task<UpdateFileResponse> Handle(UpdateFileCommand request, CancellationToken cancellationToken)
    {
        var container = _blobStorageService.ForContainer();
        var isDeleted = await container.DeleteAsync(request.FileName, cancellationToken);

        if (!isDeleted)
        {
            return new UpdateFileResponse
            {
                IsSuccess = false,
                Message = "File not found or couldn't be deleted."
            };
        }

        var newFileName = $"{Guid.NewGuid()}_{Path.GetFileName(request.File.FileName)}";

        using var stream = request.File.OpenReadStream();
        var fileLink = await container.UploadAsync(newFileName, stream, request.File.ContentType ?? "application/octet-stream", cancellationToken);

        return new UpdateFileResponse
        {
            IsSuccess = true,
            FileLink = fileLink,
            Message = "File updated successfully."
        };
    }
}
