using Microsoft.AspNetCore.Identity;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Crm;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.CreateReservation
{
    public class CreateReservationHandler : IRequestHandler<CreateReservationCommand, CreateReservationResponse>
    {
        private readonly IReservationRepository _reservationRepository;
        private readonly IUnitRepository _unitRepository;
        private readonly UserManager<User> _userManager;
        private readonly IUnitStatusService _unitStatus;
        private readonly ApplicationDbContext _db;
        private readonly IContactResolver _contacts;

        public CreateReservationHandler(
            IReservationRepository reservationRepository,
            IUnitRepository unitRepository,
            UserManager<User> userManager,
            IUnitStatusService unitStatus,
            ApplicationDbContext db,
            IContactResolver contacts)
        {
            _reservationRepository = reservationRepository;
            _unitRepository = unitRepository;
            _userManager = userManager;
            _unitStatus = unitStatus;
            _db = db;
            _contacts = contacts;
        }
    
        public async Task<CreateReservationResponse> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate Unit exists
                var unit = await _unitRepository.GetByIDAsync(request.UnitId)
                    ?? throw new NotFoundException($"Unit with ID '{request.UnitId}' not found.");

                // Spec §7.7 — a unit may only carry ONE active reservation. The
                // blocking set is owned by ReservationStateMachine and mirrored by
                // the filtered index IX_Reservations_ActivePerUnit, which is the
                // authoritative backstop for concurrent requests (see ADR-0002).
                // A DRAFT deliberately does not block the unit (§12.1).
                var activeForUnit = await _reservationRepository.Find(r =>
                    r.UnitId == request.UnitId &&
                    (r.Status == ReservationStatus.Pending ||
                     r.Status == ReservationStatus.ChangesRequested ||
                     r.Status == ReservationStatus.Approved ||
                     r.Status == ReservationStatus.Sold));

                if (activeForUnit.Any())
                {
                    throw BusinessRuleException.UnitNotAvailable(request.UnitId);
                }

                // §3 — only an AVAILABLE unit is selectable. Checking the unit's own
                // status as well as the reservation table catches a unit that was
                // suspended or cancelled administratively without a live reservation.
                if (!UnitStateMachine.IsSelectable(unit.Status))
                {
                    throw BusinessRuleException.UnitNotAvailable(request.UnitId);
                }

                // Validate Agent exists (only agent validation required, buyerId is trusted)
                var agent = await _userManager.FindByIdAsync(request.AgentId)
                    ?? throw new NotFoundException($"Agent with ID '{request.AgentId}' not found.");

                // §1.1 — the buyer is a person, not a copy of their name. Resolve
                // (or create) the one CrmContact they are, so the same human is
                // recognisable across reservations instead of being duplicated
                // inline on each one. An account is optional and stays optional.
                var contact = await _contacts.ResolveAsync(
                    request.Name, request.LastName, request.Email, request.PhoneNumber,
                    request.CIN, request.BuyerId, cancellationToken);

                // Create reservation - buyerId and notaireId are stored as-is without validation
                var reservation = new Reservation
                {
                    Id = Guid.NewGuid(),
                    PrimaryContact = contact,
                    BuyerId = request.BuyerId,
                    Name = request.Name ?? string.Empty,
                    LastName = request.LastName ?? string.Empty,
                    CIN = request.CIN,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    UnitId = request.UnitId,
                    AgentId = request.AgentId,
                    NotaireId = request.NotaireId,
                    TotalPropertyPrice = request.TotalPropertyPrice,
                    ReservationAmount = request.ReservationAmount,
                    ReservationDate = DateTime.Now,
                    IsUnderConstruction = request.IsUnderConstruction,
                    UnitDetails = $"{unit.UnitNumber} - {unit.TotalSurface}m²",
                    Status = ReservationStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                // §3 — the reservation row and the unit hold are one atomic act.
                // Committing the reservation without the hold (or vice versa) is
                // exactly the drift that left units AVAILABLE while reserved.
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                await _reservationRepository.InsertAsync(reservation);

                await _unitStatus.TransitionAsync(
                    request.UnitId,
                    UnitCommercialStatus.HoldPendingApproval,
                    UnitStatusCause.ReservationSubmitted,
                    reservationId: reservation.Id,
                    actorUserId: request.AgentId,
                    ct: cancellationToken);

                await _reservationRepository.SaveAsync();
                await transaction.CommitAsync(cancellationToken);

                var referencedEntities = new List<string>();
                if (!string.IsNullOrEmpty(request.BuyerId))
                {
                    referencedEntities.Add($"BuyerId: {request.BuyerId}");
                }
                if (!string.IsNullOrEmpty(request.NotaireId))
                {
                    referencedEntities.Add($"NotaireId: {request.NotaireId}");
                }

                return new CreateReservationResponse
                {
                    ReservationId = reservation.Id,
                    Message = "Reservation created successfully (Pending validation).",
                    Success = true,
                    Details = $"Created for Unit ID {request.UnitId} by Agent {agent.Email}"
                };
            }
            catch (ProjectAPI.Api.Application.Common.Exceptions.ValidationException ve)
            {
                var errorMessages = ve.Errors.SelectMany(e => e.Value.Select(msg => $"{e.Key}: {msg}")).ToList();
                return new CreateReservationResponse
                {
                    Success = false,
                    Message = "Validation failed: " + string.Join("; ", errorMessages)
                };
            }
            // BusinessRuleException and NotFoundException are deliberately NOT caught:
            // ApiExceptionFilter turns them into the correct HTTP status and stable
            // error code (spec §31.7). Swallowing them here would return 200 with
            // success=false and hide a real conflict from the client.
        }
    }
}