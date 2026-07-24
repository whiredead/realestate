namespace ProjectAPI.Api.Application.Quartiers.GetQuartierById;

/// <summary>
/// Query to retrieve a quartier by ID.
/// </summary>
public class GetQuartierByIdQuery : IRequest<GetQuartierByIdResponse>
{
    public Guid Id { get; set; }
}
