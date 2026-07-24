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

        public CreateTypeBienHandler(ITypeBienRepository typeBienRepository)
        {
            _typeBienRepository = typeBienRepository;
        }

        public async Task<CreateTypeBienResponse> Handle(CreateTypeBienCommand request, CancellationToken cancellationToken)
        {
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
                MinSurface = request.MinSurface,
                MaxSurface = request.MaxSurface,
                ImagesInterieur = request.ImagesInterieur
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
