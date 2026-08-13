using MediatR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Application.Projects.RemoveProject;

public class RemoveProjectHandler : IRequestHandler<RemoveProjectCommand, RemoveProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IImmeubleRepository _immeubleRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly ProjectScopeService _projectScope;

    public RemoveProjectHandler(
        IProjectRepository projectRepository,
        IImmeubleRepository immeubleRepository,
        IAppointmentRepository appointmentRepository,
        IReservationRepository reservationRepository,
        ISaleRepository saleRepository,
        IUnitRepository unitRepository,
        ProjectScopeService projectScope)
    {
        _projectRepository = projectRepository;
        _immeubleRepository = immeubleRepository;
        _appointmentRepository = appointmentRepository;
        _reservationRepository = reservationRepository;
        _saleRepository = saleRepository;
        _unitRepository = unitRepository;
        _projectScope = projectScope;
    }

    public async Task<RemoveProjectResponse> Handle(RemoveProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var deletedEntities = new List<string>();

            // Step 1: Get the project
            var project = await _projectRepository.GetByIDAsync(request.ProjectId);
            if (project == null)
            {
                return new RemoveProjectResponse
                {
                    Success = false,
                    Message = $"Project with ID '{request.ProjectId}' not found.",
                    Details = new List<string> { "Please verify the project ID and try again." }
                };
            }

            // §6.4 — the single highest-blast-radius mutation in the codebase:
            // an unscoped hard-delete of a project and everything under it
            // (immeubles, units, reservations, sales, appointments). Must
            // never run outside the caller's own assigned project.
            await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

            var projectName = project.Name;

            // Step 2: Get and delete all immeubles for this project
            var allImmeubles = await _immeubleRepository.GetAllAsync();
            var projectImmeubles = allImmeubles.Where(i => i.ProjectId == request.ProjectId).ToList();

            if (projectImmeubles.Any())
            {
                deletedEntities.Add($"Found {projectImmeubles.Count} Immeuble(s) in project");
            }

            // Step 3: Get all units for these immeubles
            var allUnits = await _unitRepository.GetAllAsync();
            var unitsInProject = allUnits.Where(u => projectImmeubles.Select(i => i.Id).Contains(u.ProjectId)).ToList();

            // Step 4: Delete items in specific order (child -> parent)

            // 4a. Delete appointments
            var allAppointments = await _appointmentRepository.GetAllAsync();
            var appointmentsToDelete = allAppointments.Where(a => a.ProjectId == request.ProjectId).ToList();
            foreach (var appt in appointmentsToDelete)
            {
                _appointmentRepository.Delete(appt);
                deletedEntities.Add($"Appointment ID: {appt.Id}");
            }
            await _appointmentRepository.SaveAsync();

            // 4b. Delete sales
            var allSales = await _saleRepository.GetAllAsync();
            var unitIds = unitsInProject.Select(u => u.Id).ToList();
            var salesToDelete = allSales.Where(s => unitIds.Contains(s.UnitId)).ToList();
            foreach (var sale in salesToDelete)
            {
                _saleRepository.Delete(sale);
                deletedEntities.Add($"Sale ID: {sale.Id}");
            }
            await _saleRepository.SaveAsync();

            // 4c. Delete reservations
            var allReservations = await _reservationRepository.GetAllAsync();
            var reservationsToDelete = allReservations.Where(r => unitIds.Contains(r.UnitId)).ToList();
            foreach (var res in reservationsToDelete)
            {
                _reservationRepository.Delete(res);
                deletedEntities.Add($"Reservation ID: {res.Id}");
            }
            await _reservationRepository.SaveAsync();

            // 4d. Delete units
            foreach (var unit in unitsInProject)
            {
                _unitRepository.Delete(unit);
                deletedEntities.Add($"Unit ID: {unit.Id} (Number: {unit.UnitNumber})");
            }
            await _unitRepository.SaveAsync();

            // 4e. Delete immeubles
            foreach (var immeuble in projectImmeubles)
            {
                _immeubleRepository.Delete(immeuble);
                deletedEntities.Add($"Immeuble ID: {immeuble.Id} (Name: {immeuble.Name})");
            }
            await _immeubleRepository.SaveAsync();

            // Step 5: Delete the project itself
            await _projectRepository.DeleteAsync(project.Id);
            await _projectRepository.SaveAsync();
            deletedEntities.Add($"Project ID: {project.Id} (Name: {projectName})");

            return new RemoveProjectResponse
            {
                Success = true,
                Message = $"Project '{projectName}' deleted successfully with all related entities.",
                DeletedEntities = deletedEntities,
                Details = new List<string>
                {
                    $"Total entities deleted: {deletedEntities.Count}",
                    $"Deleted at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
                }
            };
        }
        catch (BusinessRuleException)
        {
            // §6.4 — a scope denial is an authorization decision (403), not a
            // soft "operation failed" outcome; let it reach the global
            // exception filter like every other scope check in this codebase.
            throw;
        }
        catch (Exception ex)
        {
            return new RemoveProjectResponse
            {
                Success = false,
                Message = $"Error deleting project ID '{request.ProjectId}': {ex.Message}",
                Details = new List<string>
                {
                    $"Exception Type: {ex.GetType().Name}",
                    $"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    $"Full Error: {ex.ToString()}"
                }
            };
        }
    }
}