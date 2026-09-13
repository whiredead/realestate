using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Application.TypeBiens.UpdateTypeBien;

/// <summary>
/// Handler for updating a TypeBien entity.
/// </summary>
public class UpdateTypeBienHandler : IRequestHandler<UpdateTypeBienCommand, UpdateTypeBienResponse>
{
    private readonly ITypeBienRepository _typeBienRepository;
    private readonly Common.Media.MediaUrlPolicy _media;

    public UpdateTypeBienHandler(
        ITypeBienRepository typeBienRepository,
        Common.Media.MediaUrlPolicy media)
    {
        _typeBienRepository = typeBienRepository;
        _media = media;
    }

    public async Task<UpdateTypeBienResponse> Handle(UpdateTypeBienCommand request, CancellationToken cancellationToken)
    {
        var typeBien = await _typeBienRepository.GetByIDAsync(request.Id);
        if (typeBien == null)
        {
            throw new NotFoundException($"TypeBien {request.Id} not found.");
        }

        // §7.2 — only the links the caller is actually changing are checked, so
        // a type whose media predates this policy stays editable (§9).
        if (request.Image is not null && request.Image != typeBien.Image)
        {
            _media.EnsureImageUrl(request.Image, "Image");
        }
        if (request.ImagesInterieur is not null && request.ImagesInterieur != typeBien.ImagesInterieur)
        {
            _media.EnsureImageUrls(
                TypeBienMedia.SplitImages(request.ImagesInterieur),
                "ImagesInterieur",
                TypeBienMedia.SplitImages(typeBien.ImagesInterieur));
        }
        if (request.Module3DLink is not null && request.Module3DLink != typeBien.Module3DLink)
        {
            _media.Ensure3DLink(request.Module3DLink, "Module3DLink");
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
            { () => request.NbrDouche.HasValue, () => typeBien.NbrDouche = request.NbrDouche },
            { () => request.NbrParking.HasValue, () => typeBien.NbrParking = request.NbrParking },
            { () => request.Module3DLink != null, () => typeBien.Module3DLink = request.Module3DLink },
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

        await _typeBienRepository.Update(typeBien);
        await _typeBienRepository.SaveAsync();

        return new UpdateTypeBienResponse
        {
            IsSuccess = true,
            Message = "TypeBien updated successfully."
        };
    }
}