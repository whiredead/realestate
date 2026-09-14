using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.TypeBiens.CreateTypeBien
{
    /// <summary>
    /// Handler for creating a new TypeBien entity.
    /// </summary>
    public class CreateTypeBienHandler : IRequestHandler<CreateTypeBienCommand, CreateTypeBienResponse>
    {
        private readonly ITypeBienRepository _typeBienRepository;
        private readonly Common.Media.MediaUrlPolicy _media;

        public CreateTypeBienHandler(
            ITypeBienRepository typeBienRepository,
            Common.Media.MediaUrlPolicy media)
        {
            _typeBienRepository = typeBienRepository;
            _media = media;
        }

        public async Task<CreateTypeBienResponse> Handle(CreateTypeBienCommand request, CancellationToken cancellationToken)
        {
            // Same rule as the update validator: a range whose minimum exceeds
            // its maximum is not a range. Creation used to accept it.
            if (request.MinSurface.HasValue && request.MaxSurface.HasValue && request.MinSurface > request.MaxSurface)
            {
                throw new Common.Exceptions.ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure("MaxSurface", "La surface maximale doit être supérieure ou égale à la surface minimale.")
                });
            }
            if (request.MinSurface is < 0 || request.MaxSurface is < 0)
            {
                throw new Common.Exceptions.ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure("MinSurface", "Les surfaces ne peuvent pas être négatives.")
                });
            }

            // §7.2 — reference data feeds the public catalogue like everything else.
            _media.EnsureImageUrl(request.Image, "Image");
            _media.EnsureImageUrls(TypeBienMedia.SplitImages(request.ImagesInterieur), "ImagesInterieur");
            _media.Ensure3DLink(request.Module3DLink, "Module3DLink");

            // Construct the new TypeBien entity
            var entity = new TypeBien
            {
                // Id is auto-generated if it's an identity, else set manually
                Name = request.Name,
                Description = request.Description,
                Image = request.Image,
                Price = request.Price,
                NbrChambre = request.NbrChambre,
                NbrSalleDeBain = request.NbrSalleDeBain,
                NbrDouche = request.NbrDouche,
                NbrParking = request.NbrParking,
                MinSurface = request.MinSurface,
                MaxSurface = request.MaxSurface,
                ImagesInterieur = request.ImagesInterieur,
                Module3DLink = request.Module3DLink
            };

            // Insert and save
            await _typeBienRepository.InsertAsync(entity);
            await _typeBienRepository.SaveAsync();

            return new CreateTypeBienResponse
            {
                Id = entity.Id,
                Message = "TypeBien created successfully."
            };
        }
    }
}
