namespace ProjectAPI.Api.Application.Quartiers.CreateQuartier;

/// <summary>
/// Response for the CreateQuartier command.
/// </summary>
public class CreateQuartierResponse
{
    /// <summary>
    /// The unique identifier of the newly created quartier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// A message indicating the creation outcome.
    /// </summary>
    public string Message { get; set; }
}
