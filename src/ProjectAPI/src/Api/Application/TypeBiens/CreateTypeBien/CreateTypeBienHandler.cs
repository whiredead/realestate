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
