namespace ProjectAPI.Api.Application.Projects.GetQuartierAmenities;

public class QuartierAmenityResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class GetQuartierAmenitiesQuery : IRequest<List<QuartierAmenityResponse>>
{
    public Guid ProjectId { get; set; }
}
