namespace ProjectAPI.Api.Application.Quartiers.GetQuartierById;

/// <summary>
/// Response for retrieving a quartier by ID.
/// </summary>
public class GetQuartierByIdResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? City { get; set; }
    public string Images { get; set; }
}
