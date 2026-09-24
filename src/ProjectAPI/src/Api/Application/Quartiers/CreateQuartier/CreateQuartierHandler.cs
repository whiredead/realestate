using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Quartiers.CreateQuartier;

/// <summary>
/// Handler for creating a new quartier.
/// </summary>
public class CreateQuartierHandler : IRequestHandler<CreateQuartierCommand, CreateQuartierResponse>
{
    private readonly IQuartierRepository _quartierRepository;

    public CreateQuartierHandler(IQuartierRepository quartierRepository)
    {
        _quartierRepository = quartierRepository;
    }

    public async Task<CreateQuartierResponse> Handle(CreateQuartierCommand request, CancellationToken cancellationToken)
    {
        // A second quartier with the same name is indistinguishable in every
        // picker and on the public site.
        var name = request.Name.Trim();
        var duplicate = (await _quartierRepository.Find(q => q.Name.ToLower() == name.ToLower())).Any();
        if (duplicate)
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Name), "Un quartier porte déjà ce nom.")
            });
        }

        // Create the Quartier entity
        var quartier = new Quartier
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description,
            Images = request.Images
        };

        await _quartierRepository.InsertAsync(quartier);
        await _quartierRepository.SaveAsync();

        return new CreateQuartierResponse
        {
            Id = quartier.Id,
            Message = "Quartier created successfully."
        };
    }
}
