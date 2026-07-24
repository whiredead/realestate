using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.TypeBiens.AssociateToImmeuble
{
    /// <summary>
    /// Handler to link a TypeBien to an Immeuble.
    /// </summary>
    public class AssociateTypeBienToImmeubleHandler : IRequestHandler<AssociateTypeBienToImmeubleCommand, AssociateTypeBienToImmeubleResponse>
    {
        private readonly IImmeubleRepository _immeubleRepository;
        private readonly ITypeBienRepository _typeBienRepository;
        private readonly IImmeubleTypeBienRepository _immeubleTypeBienRepository;

        public AssociateTypeBienToImmeubleHandler(
            IImmeubleRepository immeubleRepository,
            ITypeBienRepository typeBienRepository,
            IImmeubleTypeBienRepository immeubleTypeBienRepository)
        {
            _immeubleRepository = immeubleRepository;
            _typeBienRepository = typeBienRepository;
            _immeubleTypeBienRepository = immeubleTypeBienRepository;
        }

        public async Task<AssociateTypeBienToImmeubleResponse> Handle(AssociateTypeBienToImmeubleCommand request, CancellationToken cancellationToken)
        {
            // Validate Immeuble
            var immeuble = await _immeubleRepository.GetByIDAsync(request.ImmeubleId);
            if (immeuble == null)
            {
                throw new NotFoundException($"Immeuble with ID {request.ImmeubleId} not found.");
            }

            // Validate TypeBien
            var typeBien = await _typeBienRepository.GetByIDAsync(request.TypeBienId);
            if (typeBien == null)
            {
                throw new NotFoundException($"TypeBien with ID {request.TypeBienId} not found.");
            }

            // Create the association
            var association = new ImmeubleTypeBien
            {
                Id = Guid.NewGuid(),
                ImmeubleId = request.ImmeubleId,
                TypeBienId = request.TypeBienId
            };

            await _immeubleTypeBienRepository.InsertAsync(association);
            await _immeubleTypeBienRepository.SaveAsync();

            return new AssociateTypeBienToImmeubleResponse
            {
                ImmeubleTypeBienId = association.Id,
                Message = "TypeBien associated with Immeuble successfully."
            };
        }
    }
}
