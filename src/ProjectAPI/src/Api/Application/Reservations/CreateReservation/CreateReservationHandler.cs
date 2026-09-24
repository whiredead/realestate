using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Crm;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Entities;
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
        private readonly Common.Reservations.ReservationDocumentChecklist _documents;
        private readonly Common.Crm.IAccountInvitationService _accountInvitations;
        private readonly ILogger<CreateReservationHandler> _logger;

        public CreateReservationHandler(
            IReservationRepository reservationRepository,
            IUnitRepository unitRepository,
            UserManager<User> userManager,
            IUnitStatusService unitStatus,
            ApplicationDbContext db,
            IContactResolver contacts,
            ProjectScopeService projectScope,
            Common.Reservations.ReservationDocumentChecklist documents,
            Common.Crm.IAccountInvitationService accountInvitations,
            ILogger<CreateReservationHandler> logger)
        {
            _accountInvitations = accountInvitations;
            _logger = logger;
            _reservationRepository = reservationRepository;
            _unitRepository = unitRepository;
            _userManager = userManager;
            _unitStatus = unitStatus;
            _db = db;
            _contacts = contacts;
            _projectScope = projectScope;
            _documents = documents;
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

                // A reservation opens a commercial file on a project still on the market: sold off-plan
                // (SUR_PLAN) or in delivery (EN_LIVRAISON). A finalised project accepts nothing at all.
                var projectStatus = await _db.Set<Project>()
                    .Where(p => p.Id == immeubleProjectId)
                    .Select(p => p.StatusGlobal)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!ProjectStatusCodes.AllowsNewReservation(projectStatus))
                {
                    throw new BusinessRuleException(
                        BusinessErrorCodes.InvalidStatusTransition,
                        "Ce projet n'accepte plus de nouvelle réservation " +
                        $"(phase actuelle : {ProjectStatusCodes.GetBusinessPhase(projectStatus)}).",
                        StatusCodes.Status409Conflict);
                }

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
                // This applies to drafts too: a draft that could be opened on a
                // sold unit would be a file that can never be submitted.
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
                var contact = request.ProspectContactId is Guid prospectId
                    ? await _db.CrmContacts.FirstOrDefaultAsync(c => c.Id == prospectId && c.ArchivedAt == null, cancellationToken)
                        ?? throw new NotFoundException($"Prospect {prospectId} not found.")
                    : await _contacts.ResolveAsync(
                        request.Name, request.LastName, request.Email, request.PhoneNumber,
                        request.CIN, request.BuyerId, cancellationToken);

                // A prospect who already has an account is linked to it, so approving the reservation upgrades that
                // account; someone without one gets an activation link below.
                var effectiveBuyerId = string.IsNullOrEmpty(request.BuyerId) ? contact.UserId : request.BuyerId;

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
                    BuyerId = effectiveBuyerId,
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
                    Status = request.CreateAsDraft ? ReservationStatus.Draft : ReservationStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    // §12.3 — every SUBMITTED reservation gets a deadline; the
                    // scheduled expiry job only acts on rows that have one. A
                    // draft holds nothing, so it has nothing to expire: giving
                    // it a deadline would have the job release a unit the draft
                    // never took.
                    ExpiresAt = request.CreateAsDraft ? null : DateTime.UtcNow.Add(DefaultHoldDuration)
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
                // §12.1 — a project's required documents are checked at the
                // moment of SUBMISSION, and this path submits immediately. A
                // brand-new reservation carries no documents yet, so when the
                // project requires any, the file has to start as a draft: upload
                // against its id, then submit. Saying that is far more use than
                // the bare "document manquant" the generic check would give,
                // which would look like the agent forgot something they never
                // had the chance to attach.
                if (!request.CreateAsDraft && immeubleProjectId != Guid.Empty)
                {
                    var required = await _documents.RequiredLabelsAsync(immeubleProjectId, cancellationToken);
                    if (required.Count > 0)
                    {
                        throw BusinessRuleException.MissingRequiredDocument(
                            $"{string.Join(", ", required)} — créez d'abord un brouillon, " +
                            "joignez les pièces, puis soumettez");
                    }
                }

                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                await _reservationRepository.InsertAsync(reservation);

                // §12.1 — a DRAFT does not block its unit. The hold is taken when
                // the file is submitted (ResubmitReservationHandler), not when it
                // is opened.
                if (!request.CreateAsDraft)
                {
                    await _unitStatus.TransitionAsync(
                        request.UnitId,
                        UnitCommercialStatus.HoldPendingApproval,
                        UnitStatusCause.ReservationSubmitted,
                        reservationId: reservation.Id,
                        actorUserId: request.AgentId,
                        ct: cancellationToken);
                }

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

                // The buyer chooses their own password: when the person has no account yet, issue the activation
                // link now, so the agent can hand it over as soon as the reservation exists. The account itself is
                // created when the buyer sets the password (nothing is ever stored without one). Never fails the
                // reservation: a missing e-mail or a failed invitation just means no link is returned.
                string? activationToken = null;
                DateTime? activationExpiresAt = null;
                if (string.IsNullOrEmpty(effectiveBuyerId) && reservation.PrimaryContactId is Guid primaryContactId)
                {
                    try
                    {
                        var buyerContact = reservation.PrimaryContact
                            ?? await _db.CrmContacts.FirstOrDefaultAsync(c => c.Id == primaryContactId, cancellationToken);
                        if (buyerContact is not null
                            && !string.IsNullOrWhiteSpace(buyerContact.Email)
                            && buyerContact.UserId is null
                            && await _userManager.FindByEmailAsync(buyerContact.Email) is null)
                        {
                            var invitation = await _accountInvitations.IssueAsync(primaryContactId, cancellationToken);
                            activationToken = invitation?.Token;
                            activationExpiresAt = invitation?.ExpiresAt;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[CreateReservation] Activation link for reservation {ReservationId} could not be issued; the reservation stands.", reservation.Id);
                    }
                }

                return new CreateReservationResponse
                {
                    ReservationId = reservation.Id,
                    Message = "Reservation created successfully (Pending validation).",
                    Success = true,
                    Details = $"Created for Unit ID {request.UnitId} by Agent {agent.Email}",
                    ActivationToken = activationToken,
                    ActivationExpiresAt = activationExpiresAt
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