using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.DeleteImmeuble;

/// <summary>
/// Handler for the <see cref="DeleteImmeublesCommand"/>.
/// </summary>
public class DeleteImmeublesHandler : IRequestHandler<DeleteImmeublesCommand, DeleteImmeubleResponse>
    {
        private readonly IImmeubleRepository _immeubleRepository;
        private readonly IUnitRepository _unitRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly ProjectScopeService _projectScope;

        public DeleteImmeublesHandler(
            IImmeubleRepository immeubleRepository,
            IUnitRepository unitRepository,
            IAppointmentRepository appointmentRepository,
            IReservationRepository reservationRepository,
            ISaleRepository saleRepository,
            ProjectScopeService projectScope)
        {
            _immeubleRepository = immeubleRepository;
            _unitRepository = unitRepository;
            _appointmentRepository = appointmentRepository;
            _reservationRepository = reservationRepository;
            _saleRepository = saleRepository;
            _projectScope = projectScope;
        }

/// <summary>
    /// Handles the request to delete an immeuble with cascade deletion of all related entities.
    /// </summary>
    /// <param name="request">The request containing the immeuble ID to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A response indicating success or failure with details of deleted entities.</returns>
    public async Task<DeleteImmeubleResponse> Handle(DeleteImmeublesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var deletedEntities = new List<string>();

            // Get the immeuble
            var immeuble = await _immeubleRepository.GetByIdWithDependenciesAsync(request.Id);

            if (immeuble == null)
            {
                return new DeleteImmeubleResponse
                {
                    Success = false,
                    Message = $"Immeuble with ID '{request.Id}' not found.",
                    Details = new List<string> { "Please verify the Immeuble ID and try again." }
                };
            }

            // §6.4 — this is the highest-blast-radius mutation in the codebase
            // (cascades through Sales → Reservations → Units → Immeuble); it
            // must never run outside the caller's own assigned project.
            await _projectScope.EnsureProjectAccessAsync(immeuble.ProjectId, cancellationToken);

            var immeubleName = immeuble.Name;

            // Cascade delete in specific order (child -> parent)

            // 1. Get all units in this immeuble
            var allUnits = await _unitRepository.GetAllAsync();
            var unitsInImmeuble = allUnits.Where(u => u.ProjectId == request.Id).ToList();
            var unitIds = unitsInImmeuble.Select(u => u.Id).ToList();

            // 3. Delete sales for units in this immeuble
            var allSales = await _saleRepository.GetAllAsync();
            var salesToDelete = allSales.Where(s => unitIds.Contains(s.UnitId)).ToList();
            foreach (var sale in salesToDelete)
            {
                _saleRepository.Delete(sale);
                deletedEntities.Add($"Sale ID: {sale.Id}");
            }
            await _saleRepository.SaveAsync();

            // 4. Delete reservations for units in this immeuble
            var allReservations = await _reservationRepository.GetAllAsync();
            var reservationsToDelete = allReservations.Where(r => unitIds.Contains(r.UnitId)).ToList();
            foreach (var res in reservationsToDelete)
            {
                _reservationRepository.Delete(res);
                deletedEntities.Add($"Reservation ID: {res.Id}");
            }
            await _reservationRepository.SaveAsync();

            // 5. Delete units
            foreach (var unit in unitsInImmeuble)
            {
                _unitRepository.Delete(unit);
                deletedEntities.Add($"Unit ID: {unit.Id} (Number: {unit.UnitNumber})");
            }
            await _unitRepository.SaveAsync();

            // 6. Delete the immeuble itself
            await _immeubleRepository.DeleteAsync(immeuble.Id);
            await _immeubleRepository.SaveAsync();
            deletedEntities.Add($"Immeuble ID: {immeuble.Id} (Name: {immeubleName})");

            return new DeleteImmeubleResponse
            {
                Success = true,
                Message = $"Immeuble named '{immeubleName}' (ID: {request.Id}) deleted successfully with all related entities.",
                Details = new List<string>
                {
                    $"Total entities deleted: {deletedEntities.Count}",
                    $"Deleted at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
                }
            };
        }
        catch (BusinessRuleException)
        {
            // §6.4 — a scope denial is an authorization decision (403 via
            // ApiExceptionFilter/BusinessErrorCodes.PROJECT_SCOPE_DENIED), not
            // a soft "operation failed" outcome. Swallowing it into
            // Success = false here made ImmeubleController map it to a plain
            // 400 with no stable error code, so the frontend's
            // ApiError.isScopeDenied could never recognise it. Let it
            // propagate to the global exception filter like every other
            // authorization check in this codebase.
            throw;
        }
        catch (Exception ex)
        {
            return new DeleteImmeubleResponse
            {
                Success = false,
                Message = $"Error deleting immeuble with ID '{request.Id}': {ex.Message}",
                Details = new List<string>
                {
                    $"Exception Type: {ex.GetType().Name}",
                    $"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
                }
            };
        }
    }
}
