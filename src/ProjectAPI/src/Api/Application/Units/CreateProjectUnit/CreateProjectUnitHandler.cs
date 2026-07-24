using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Units.CreateProjectUnit
{
    /// <summary>
    /// Handler for creating a new project unit.
    /// </summary>
    public class CreateProjectUnitHandler : IRequestHandler<CreateProjectUnitCommand, CreateProjectUnitResponse>
    {
        private readonly IUnitRepository _unitRepository;
        private readonly IImmeubleRepository _projectRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateProjectUnitHandler"/> class.
        /// </summary>
        /// <param name="unitRepository">The repository to access unit data.</param>
        /// <param name="projectRepository">The repository to access project data.</param>
        public CreateProjectUnitHandler(IUnitRepository unitRepository, IImmeubleRepository projectRepository)
        {
            _unitRepository = unitRepository;
            _projectRepository = projectRepository;
        }

        /// <summary>
        /// Handles the command to create a new project unit.
        /// </summary>
        /// <param name="request">The <see cref="CreateProjectUnitCommand"/> containing the details of the unit to be created.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation, containing the <see cref="CreateProjectUnitResponse"/>.</returns>
        /// <exception cref="NotFoundException">Thrown when the specified project is not found.</exception>
        public async Task<CreateProjectUnitResponse> Handle(CreateProjectUnitCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Check if the project exists
                var project = await _projectRepository.GetByIDAsync(request.ProjectId);
                if (project == null)
                {
                    throw new NotFoundException("Project not found.");
                }

                // Create a new unit
                var unit = new Domain.Immeubles.Entities.Unit
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId,
                    Floor = request.Floor,
                    UnitNumber = request.UnitNumber,
                    NumberOfBedrooms = request.NumberOfBedrooms,
                    NumberOfBathrooms = request.NumberOfBathrooms,
                    ApartmentSurface = request.ApartmentSurface,
                    BalconySurface = request.BalconySurface,
                    TerraceSurface = request.TerraceSurface,
                    GardenSurface = request.GardenSurface,
                    View = request.View,
                    Orientation = request.Orientation,
                    TotalSurface = request.TotalSurface
                };

                // Insert the unit
                await _unitRepository.InsertAsync(unit);
                await _unitRepository.SaveAsync();

                return new CreateProjectUnitResponse
                {
                    UnitId = unit.Id,
                    Message = "Project unit added successfully."
                };
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }

        }
    }
}