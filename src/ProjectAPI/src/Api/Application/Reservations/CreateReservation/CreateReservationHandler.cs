using Microsoft.AspNetCore.Identity;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Reservations.CreateReservation
{
    public class CreateReservationHandler : IRequestHandler<CreateReservationCommand, CreateReservationResponse>
    {
        private readonly IReservationRepository _reservationRepository;
        private readonly IUnitRepository _unitRepository;
        private readonly UserManager<User> _userManager;
    
        public CreateReservationHandler(
            IReservationRepository reservationRepository,
            IUnitRepository unitRepository,
            UserManager<User> userManager)
        {
            _reservationRepository = reservationRepository;
            _unitRepository = unitRepository;
            _userManager = userManager;
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

                // Validate Agent exists (only agent validation required, buyerId is trusted)
                var agent = await _userManager.FindByIdAsync(request.AgentId)
                    ?? throw new NotFoundException($"Agent with ID '{request.AgentId}' not found.");

                // Create reservation - buyerId and notaireId are stored as-is without validation
                var reservation = new Reservation
                {
                    Id = Guid.NewGuid(),
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

                await _reservationRepository.InsertAsync(reservation);
                await _reservationRepository.SaveAsync();

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