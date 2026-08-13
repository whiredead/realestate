using MediatR;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.TypeBiens.AssociateToProject
{
    /// <summary>
    /// Handler to link a TypeBien to a Project.
    ///
    /// </summary>
    public class AssociateTypeBienToProjectHandler : IRequestHandler<AssociateTypeBienToProjectCommand, AssociateTypeBienToProjectResponse>
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ITypeBienRepository _typeBienRepository;
        private readonly IProjectTypeBienRepository _projectTypeBienRepository;
        private readonly ProjectScopeService _projectScope;

        public AssociateTypeBienToProjectHandler(
            IProjectRepository projectRepository,
            ITypeBienRepository typeBienRepository,
            IProjectTypeBienRepository projectTypeBienRepository,
            ProjectScopeService projectScope)
        {
            _projectRepository = projectRepository;
            _typeBienRepository = typeBienRepository;
            _projectTypeBienRepository = projectTypeBienRepository;
            _projectScope = projectScope;
        }

        public async Task<AssociateTypeBienToProjectResponse> Handle(AssociateTypeBienToProjectCommand request, CancellationToken cancellationToken)
        {
            // Validate Project
            var project = await _projectRepository.GetByIDAsync(request.ProjectId);
            if (project == null)
            {
                throw new NotFoundException($"Project with ID {request.ProjectId} not found.");
            }

            // §6.4 — a PROJECT_ADMIN with no membership on this project must
            // not alter its catalogue composition.
            await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

            // Validate TypeBien
            var typeBien = await _typeBienRepository.GetByIDAsync(request.TypeBienId);
            if (typeBien == null)
            {
                throw new NotFoundException($"TypeBien with ID {request.TypeBienId} not found.");
            }

            // Check if association already exists
            var allAssociations = await _projectTypeBienRepository.GetAllAsync();
            var existingAssociation = allAssociations.Where(
                ptb => ptb.ProjectId == request.ProjectId && ptb.TypeBienId == request.TypeBienId);
            
            if (existingAssociation.Any())
            {
                return new AssociateTypeBienToProjectResponse
                {
                    ProjectTypeBienId = existingAssociation.First().Id,
                    Message = "TypeBien is already associated with this Project."
                };
            }

            // Create the association
            var association = new ProjectTypeBien
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                TypeBienId = request.TypeBienId
            };

            await _projectTypeBienRepository.InsertAsync(association);
            await _projectTypeBienRepository.SaveAsync();

            return new AssociateTypeBienToProjectResponse
            {
                ProjectTypeBienId = association.Id,
                Message = "TypeBien associated with Project successfully."
            };
        }
    }
}