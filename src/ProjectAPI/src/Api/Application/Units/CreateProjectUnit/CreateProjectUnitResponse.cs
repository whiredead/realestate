namespace ProjectAPI.Api.Application.Units.CreateProjectUnit;

/// <summary>
/// Response for adding a new project unit.
/// </summary>
public class CreateProjectUnitResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the newly created unit.
    /// </summary>
    public Guid UnitId { get; set; }

    /// <summary>
    /// Gets or sets the message providing details about the creation result.
    /// </summary>
    public string Message { get; set; }
}