using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.TypeBiens.UpdateTypeBien;

/// <summary>
/// Handler for updating a TypeBien entity.
/// </summary>
public class UpdateTypeBienHandler : IRequestHandler<UpdateTypeBienCommand, UpdateTypeBienResponse>
{
    private readonly ITypeBienRepository _typeBienRepository;

    public UpdateTypeBienHandler(ITypeBienRepository typeBienRepository)
    {
        _typeBienRepository = typeBienRepository;
    }

    public async Task<UpdateTypeBienResponse> Handle(UpdateTypeBienCommand request, CancellationToken cancellationToken)
    {
        var typeBien = await _typeBienRepository.GetByIDAsync(request.Id);
        if (typeBien == null)
        {
            return new UpdateTypeBienResponse
            {
                IsSuccess = false,
                Message = "TypeBien not found."
            };
        }

        // Dictionary of updates to apply only if the field is filled
        var updateActions = new Dictionary<Func<bool>, Action>
        {
            { () => request.Name != null, () => typeBien.Name = request.Name },
            { () => request.Description != null, () => typeBien.Description = request.Description },
            { () => request.Image != null, () => typeBien.Image = request.Image },
            { () => request.Price.HasValue, () => typeBien.Price = request.Price },
            { () => request.NbrChambre.HasValue, () => typeBien.NbrChambre = request.NbrChambre },
            { () => request.NbrSalleDeBain.HasValue, () => typeBien.NbrSalleDeBain = request.NbrSalleDeBain },
            { () => request.MinSurface.HasValue, () => typeBien.MinSurface = request.MinSurface },
            { () => request.MaxSurface.HasValue, () => typeBien.MaxSurface = request.MaxSurface },
            { () => request.ImagesInterieur != null, () => typeBien.ImagesInterieur = request.ImagesInterieur }
        };

        // Apply only the updates where the condition is met
        foreach (var updateAction in updateActions)
        {
            if (updateAction.Key.Invoke())
            {
                updateAction.Value.Invoke();
            }
        }

        _typeBienRepository.Update(typeBien);
        await _typeBienRepository.SaveAsync();

        return new UpdateTypeBienResponse
        {
            IsSuccess = true,
            Message = "TypeBien updated successfully."
        };
    }
}