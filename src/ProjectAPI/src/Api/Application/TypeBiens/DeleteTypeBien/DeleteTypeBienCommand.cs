namespace ProjectAPI.Api.Application.TypeBiens.DeleteTypeBien;

/// <summary>
/// Command to delete a TypeBien entity.
/// </summary>
public class DeleteTypeBienCommand : IRequest<DeleteTypeBienResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier of the TypeBien to be deleted.
    /// </summary>
    public int Id { get; set; }
}