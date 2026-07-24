namespace ProjectAPI.Api.Application.TypeBiens.UpdateTypeBien;

/// <summary>
/// Response for updating a TypeBien.
/// </summary>
public class UpdateTypeBienResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the update was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the message describing the result of the update operation.
    /// </summary>
    public string Message { get; set; }
}