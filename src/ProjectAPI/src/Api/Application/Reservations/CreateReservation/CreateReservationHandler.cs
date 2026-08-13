using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Crm;
using ProjectAPI.Api.Application.Common.Security;
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
        /// <summary>
        /// §12.3 — default hold window before a SUBMITTED reservation expires
        /// and its unit is released. No project-level override exists yet, so
        /// every reservation gets the same deadline.
        /// </summary>
        private static readonly TimeSpan DefaultHoldDuration = TimeSpan.FromHours(72);

        private readonly IReservationRepository _reservationRepository;
        private readonly IUnitRepository _unitRepository;
        private readonly UserManager<User> _userManager;
        private readonly IUnitStatusService _unitStatus;
        private readonly ApplicationDbContext _db;
        private readonly IContactResolver _contacts;
        private readonly ProjectScopeService _projectScope;

        public CreateReservationHandler(
            IReservationRepository reservationRepository,
            IUnitRepository unitRepository,
            UserManager<User> userManager,
            IUnitStatusService unitStatus,
            ApplicationDbContext db,
            IContactResolver contacts,
            ProjectScopeService projectScope)
        {
            _reservationRepository = reservationRepository;
            _unitRepository = unitRepository;
            _userManager = userManager;
            _unitStatus = unitStatus;
            _db = db;
            _contacts = contacts;
            _projectScope = projectScope;
        }
    
        public async Task<CreateReservationResponse> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate Unit exists
                var unit = await _unitRepository.GetByIDAsync(request.UnitId)
                    ?? throw new NotFoundException($"Unit with ID '{request.UnitId}' not found.");

                // §6.4 — an agent may only create a reservation on a unit that
                // belongs to one of their assigned projects. unit.ProjectId is
                // the Immeuble FK (see Unit.cs); the Immeuble carries the real
                // project id that ProjectMembership scopes against.
                var immeubleProjectId = await _db.Set<Immeuble>()
                    .Where(im => im.Id == unit.ProjectId)
                    .Select(im => im.ProjectId)
                    .FirstOrDefaultAsync(cancellationToken);
                await _projectScope.EnsureProjectAccessAsync(immeubleProjectId, cancellationToken);

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

                // §5.3 — final_price = catalog_price - discount, frozen at submit
                // and never recomputed even if the unit's catalogue price moves
                // later. Falls back to the submitted total when the unit carries
                // no catalogue price yet, so older/incomplete stock still works.
                var catalogPrice = unit.LatestPrice ?? request.TotalPropertyPrice;
                var finalPrice = catalogPrice - request.Discount;

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
                    // §5.3 — frozen at submit; never reassigned afterward.
                    OwnerSalesAgentId = request.AgentId,
                    NotaireId = request.NotaireId,
                    TotalPropertyPrice = request.TotalPropertyPrice,
                    ReservationAmount = request.ReservationAmount,
                    CatalogPrice = catalogPrice,
                    Discount = request.Discount,
                    FinalPrice = finalPrice,
                    ReservationDate = DateTime.Now,
                    IsUnderConstruction = request.IsUnderConstruction,
                    UnitDetails = $"{unit.UnitNumber} - {unit.TotalSurface}m²",
                    Status = ReservationStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    // §12.3 — every SUBMITTED reservation gets a deadline; the
                    // scheduled expiry job only acts on rows that have one.
                    ExpiresAt = DateTime.UtcNow.Add(DefaultHoldDuration)
                };

                // §5.3/§6.2 — co-buyers are distinct CrmContacts, resolved the
                // same way as the primary buyer so a repeat co-buyer is
                // recognised rather than duplicated.
                if (request.CoBuyers.Count > 0)
                {
                    foreach (var coBuyerInput in request.CoBuyers)
                    {
                        var coBuyerContact = await _contacts.ResolveAsync(
                            coBuyerInput.Name, coBuyerInput.LastName, coBuyerInput.Email, coBuyerInput.Phone,
                            coBuyerInput.Cin, ct: cancellationToken);

                        reservation.Buyers.Add(new ReservationBuyer
                        {
                            Id = Guid.NewGuid(),
                            ReservationId = reservation.Id,
                            CrmContactId = coBuyerContact.Id,
                            OwnershipPercent = coBuyerInput.OwnershipPercent
                        });
                    }

                    var allPercentages = request.CoBuyers
                        .Where(c => c.OwnershipPercent.HasValue)
                        .Select(c => c.OwnershipPercent!.Value)
                        .ToList();

                    if (allPercentages.Count > 0)
                    {
                        // The primary buyer's implicit share plus every co-buyer's
                        // explicit share must total 100% (§5.3).
                        var total = allPercentages.Sum();
                        if (allPercentages.Count == request.CoBuyers.Count)
                        {
                            // All co-buyers specified a percentage; primary buyer's
                            // share is whatever remains, must not go negative.
                            var primaryShare = 100m - total;
                            if (primaryShare < 0)
                            {
                                throw new BusinessRuleException(
                                    BusinessErrorCodes.ValidationFailed,
                                    $"La somme des pourcentages de propriété des co-acquéreurs ({total:N2}%) dépasse 100%.");
                            }
                        }
                    }
                }

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