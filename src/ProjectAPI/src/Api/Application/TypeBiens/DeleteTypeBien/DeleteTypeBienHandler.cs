using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Appointments.Interfaces;

namespace ProjectAPI.Api.Application.TypeBiens.DeleteTypeBien;

/// <summary>
/// Handler for deleting a TypeBien entity.
/// </summary>
public class DeleteTypeBienHandler : IRequestHandler<DeleteTypeBienCommand, DeleteTypeBienResponse>
{
    private readonly ITypeBienRepository _typeBienRepository;
    private readonly IProjectTypeBienRepository _projectTypeBienRepository;
    private readonly IAppointmentRepository _appointmentRepository;

    public DeleteTypeBienHandler(
        ITypeBienRepository typeBienRepository,
        IProjectTypeBienRepository projectTypeBienRepository,
        IAppointmentRepository appointmentRepository)
    {
        _typeBienRepository = typeBienRepository;
        _projectTypeBienRepository = projectTypeBienRepository;
        _appointmentRepository = appointmentRepository;
    }

    public async Task<DeleteTypeBienResponse> Handle(DeleteTypeBienCommand request, CancellationToken cancellationToken)
    {
        // Check if TypeBien exists
        var typeBien = await _typeBienRepository.GetByIDAsync(request.Id);
        if (typeBien == null)
        {
            return new DeleteTypeBienResponse
            {
                IsSuccess = false,
                Message = "TypeBien not found."
            };
        }

        // Check if TypeBien is associated with any Projects
        var allProjectTypeBiens = await _projectTypeBienRepository.GetAllAsync();
        var projectTypeBiens = allProjectTypeBiens.Where(ptb => ptb.TypeBienId == request.Id).ToList();
        if (projectTypeBiens.Any())
        {
            var projectNames = projectTypeBiens
                .Where(ptb => ptb.Project != null)
                .Select(ptb => ptb.Project.Name)
                .Distinct()
                .Take(3); // Limit to first 3 for brevity
            
            var projectList = string.Join(", ", projectNames);
            var additionalText = projectTypeBiens.Count() > 3 ? $" and {projectTypeBiens.Count() - 3} more" : "";
            
            return new DeleteTypeBienResponse
            {
                IsSuccess = false,
                Message = $"Cannot delete TypeBien. It is currently associated with {projectTypeBiens.Count()} Project(s): {projectList}{additionalText}. Please remove these associations first."
            };
        }

        // Check if TypeBien is referenced in any Appointments
        var allAppointments = await _appointmentRepository.GetAllAsync();
        var appointmentsWithThisTypeBien = allAppointments.Where(a => a.TypeBienIds.Contains(request.Id)).ToList();
        if (appointmentsWithThisTypeBien.Any())
        {
            return new DeleteTypeBienResponse
            {
                IsSuccess = false,
                Message = $"Cannot delete TypeBien. It is referenced in {appointmentsWithThisTypeBien.Count()} appointment(s). Please update these appointments first."
            };
        }

        // Safe to delete
        _typeBienRepository.Delete(typeBien);
        await _typeBienRepository.SaveAsync();

        return new DeleteTypeBienResponse
        {
            IsSuccess = true,
            Message = "TypeBien deleted successfully."
        };
    }
}