namespace ProjectAPI.Api.Application.TypeBiens.DeleteTypeBien;

/// <summary>
/// Response for deleting a TypeBien.
/// </summary>
public class DeleteTypeBienResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the deletion was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the message describing the result of the delete operation.
    /// </summary>
    public string Message { get; set; }
}