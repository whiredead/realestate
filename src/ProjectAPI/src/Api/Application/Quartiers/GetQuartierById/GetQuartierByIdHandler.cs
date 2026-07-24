using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Quartiers.GetQuartierById;

/// <summary>
/// Handler to retrieve a quartier by its ID.
/// </summary>
public class GetQuartierByIdHandler : IRequestHandler<GetQuartierByIdQuery, GetQuartierByIdResponse>
{
    private readonly IQuartierRepository _quartierRepository;

    public GetQuartierByIdHandler(IQuartierRepository quartierRepository)
    {
        _quartierRepository = quartierRepository;
    }

    public async Task<GetQuartierByIdResponse> Handle(GetQuartierByIdQuery request, CancellationToken cancellationToken)
    {
        var quartier = await _quartierRepository.GetByIDAsync(request.Id);
        if (quartier == null)
        {
            throw new NotFoundException($"Quartier with ID {request.Id} not found.");
        }

        return new GetQuartierByIdResponse
        {
            Id = quartier.Id,
            Name = quartier.Name,
            Description = quartier.Description,
            Images = quartier.Images
        };
    }
}