namespace ProjectAPI.Api.Application.Common.BlobOperations.DeleteFromBlob;

/// <summary>
/// Command for deleting a file.
/// </summary>
public class DeleteFileCommand : IRequest<DeleteFileResponse>
{
    public string FileName { get; set; }
}
