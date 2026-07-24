using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.EspacesTempsReel.GetEspaceTempsReelById
{
    /// <summary>
    /// Handler to retrieve a single EspaceTempsReel entry by ID.
    /// </summary>
    public class GetEspaceTempsReelByIdHandler : IRequestHandler<GetEspaceTempsReelByIdQuery, GetEspaceTempsReelByIdResponse>
    {
        private readonly IEspaceTempsReelRepository _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetEspaceTempsReelByIdHandler"/> class.
        /// </summary>
        /// <param name="repository">The repository for managing EspaceTempsReel data.</param>
        public GetEspaceTempsReelByIdHandler(IEspaceTempsReelRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Handles the query to retrieve a single EspaceTempsReel entry by ID.
        /// </summary>
        /// <param name="request">The query containing the EspaceTempsReel ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The EspaceTempsReel record, or an exception if not found.</returns>
        public async Task<GetEspaceTempsReelByIdResponse> Handle(GetEspaceTempsReelByIdQuery request, CancellationToken cancellationToken)
        {
            // Retrieve the entity
            var espaceTempsReel = await _repository.GetByIDAsync(request.Id);

            // If not found, throw an exception or return null (based on your domain requirements)
            if (espaceTempsReel == null)
            {
                throw new NotFoundException($"EspaceTempsReel with ID {request.Id} not found.");
            }

            // Map to the response DTO
            return new GetEspaceTempsReelByIdResponse
            {
                Id = espaceTempsReel.Id,
                VideoLink = espaceTempsReel.VideoLink,
                InsertedAt = espaceTempsReel.InsertedAt,
                ProjectId = espaceTempsReel.ProjectId
            };
        }
    }
}
