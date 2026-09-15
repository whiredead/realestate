using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Units.CreateProjectUnit
{
    /// <summary>
    /// Handler for creating a new project unit.
    /// </summary>
    public class CreateProjectUnitHandler : IRequestHandler<CreateProjectUnitCommand, CreateProjectUnitResponse>
    {
        private readonly IUnitRepository _unitRepository;
        private readonly IImmeubleRepository _projectRepository;
        private readonly ApplicationDbContext _db;
        private readonly ProjectScopeService _projectScope;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateProjectUnitHandler"/> class.
        /// </summary>
        /// <param name="unitRepository">The repository to access unit data.</param>
        /// <param name="projectRepository">The repository to access project data.</param>
        /// <param name="db">Used to validate the target floor belongs to the target building.</param>
        /// <param name="projectScope">Enforces §6.4: a unit may only be created within the caller's own assigned project.</param>
        public CreateProjectUnitHandler(
            IUnitRepository unitRepository,
            IImmeubleRepository projectRepository,
            ApplicationDbContext db,
            ProjectScopeService projectScope)
        {
            _unitRepository = unitRepository;
            _projectRepository = projectRepository;
            _db = db;
            _projectScope = projectScope;
        }

        /// <summary>
        /// Handles the command to create a new project unit.
        /// </summary>
        /// <param name="request">The <see cref="CreateProjectUnitCommand"/> containing the details of the unit to be created.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation, containing the <see cref="CreateProjectUnitResponse"/>.</returns>
        /// <exception cref="NotFoundException">Thrown when the specified building or floor is not found.</exception>
        public async Task<CreateProjectUnitResponse> Handle(CreateProjectUnitCommand request, CancellationToken cancellationToken)
        {
            // Check if the building exists. Despite the "ProjectId" naming on
            // the command, this identifies the Immeuble (Building); see the
            // Unit.ProjectId doc comment.
            var building = await _projectRepository.GetByIDAsync(request.ProjectId);
            if (building == null)
            {
                throw new NotFoundException("Building not found.");
            }

            await _projectScope.EnsureProjectAccessAsync(building.ProjectId, cancellationToken);

            var floorBelongsToBuilding = await _db.Set<Floor>()
                .AnyAsync(f => f.Id == request.FloorId && f.ImmeubleId == request.ProjectId, cancellationToken);
            if (!floorBelongsToBuilding)
            {
                throw new NotFoundException("Floor not found for this building.");
            }

            // §7.2 — a unit may only claim a layout its own project offers. The
            // FK alone would accept any type in the référentiel, which would let
            // a unit advertise a plan its project does not sell and leave it
            // invisible in the catalogue (which iterates the project's types).
            if (request.TypeBienId is int typeBienId)
            {
                var typeOfferedByProject = await _db.Set<ProjectTypeBien>()
                    .AnyAsync(
                        link => link.ProjectId == building.ProjectId && link.TypeBienId == typeBienId,
                        cancellationToken);

                if (!typeOfferedByProject)
                {
                    throw new NotFoundException("Type de bien not offered by this project.");
                }
            }

            // Create a new unit
            var unit = new Domain.Immeubles.Entities.Unit
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                FloorId = request.FloorId,
                UnitNumber = request.UnitNumber,
                NumberOfBedrooms = request.NumberOfBedrooms,
                NumberOfBathrooms = request.NumberOfBathrooms,
                ApartmentSurface = request.ApartmentSurface,
                BalconySurface = request.BalconySurface,
                TerraceSurface = request.TerraceSurface,
                GardenSurface = request.GardenSurface,
                View = request.View,
                Orientation = request.Orientation,
                TotalSurface = request.TotalSurface,
                Images = request.Images,
                TypeBienId = request.TypeBienId
            };

            // Insert the unit
            await _unitRepository.InsertAsync(unit);
            await _unitRepository.SaveAsync();

            return new CreateProjectUnitResponse
            {
                UnitId = unit.Id,
                Message = "Project unit added successfully."
            };
        }
    }
}