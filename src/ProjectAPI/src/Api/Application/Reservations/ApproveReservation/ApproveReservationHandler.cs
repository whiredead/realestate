using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectAPI.Api.Application.Common.Crm;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Application.Reservations.ApproveReservation;

public class ApproveReservationHandler : IRequestHandler<ApproveReservationCommand, bool>
{
    private readonly IReservationRepository _reservationRepo;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly ILogger<ApproveReservationHandler> _logger;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitStatusService _unitStatus;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly ProjectScopeService _projectScope;
    private readonly INotificationService _notifications;
    private readonly IAccountInvitationService _accountInvitations;
    private readonly Common.Reservations.ReservationDocumentChecklist _documents;

    public ApproveReservationHandler(
        IReservationRepository reservationRepo,
        IPurchaseRepository purchaseRepo,
        ILogger<ApproveReservationHandler> logger,
        ICurrentUser currentUser,
        IUnitStatusService unitStatus,
        ApplicationDbContext db,
        UserManager<User> userManager,
        ProjectScopeService projectScope,
        INotificationService notifications,
        IAccountInvitationService accountInvitations,
        Common.Reservations.ReservationDocumentChecklist documents)
    {
        _documents = documents;
        _reservationRepo = reservationRepo;
        _purchaseRepo = purchaseRepo;
        _logger = logger;
        _currentUser = currentUser;
        _unitStatus = unitStatus;
        _db = db;
        _userManager = userManager;
        _projectScope = projectScope;
        _notifications = notifications;
        _accountInvitations = accountInvitations;
    }

    public async Task<bool> Handle(ApproveReservationCommand request, CancellationToken ct)
    {
        _logger.LogInformation("[ApproveReservation] Starting approval for ReservationId: {ReservationId}", request.ReservationId);
        _logger.LogInformation("[ApproveReservation] Documents count in request: {Count}", request.Documents?.Count ?? 0);

        try
        {
            // §6.4 — a project admin may only approve reservations inside their
            // assigned projects; a GLOBAL_ADMIN bypasses this.
            await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

            // Use GetByIdWithDocumentsAsync to load Documents collection for adding new documents
            _logger.LogInformation("[ApproveReservation] Fetching reservation with documents...");
            var reservation = await _reservationRepo.GetByIdWithDocumentsAsync(request.ReservationId)
                ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

            _logger.LogInformation("[ApproveReservation] Reservation found. Status: {Status}, BuyerId: {BuyerId}, Existing docs count: {DocsCount}", 
                reservation.Status, reservation.BuyerId, reservation.Documents?.Count ?? 0);

            // §6.4 — separation of duties: the sales agent who submitted the
            // reservation may not approve it, even if they also hold an admin
            // role. Use the authenticated caller id, never the client-supplied
            // AdminUserId (which the caller could set to anyone).
            var callerId = _currentUser.UserId;
            if (!string.IsNullOrEmpty(callerId)
                && !string.IsNullOrEmpty(reservation.AgentId)
                && string.Equals(callerId, reservation.AgentId, StringComparison.Ordinal))
            {
                _logger.LogWarning("[ApproveReservation] Self-approval blocked: caller {Caller} is the owning agent of {ReservationId}", callerId, request.ReservationId);
                throw BusinessRuleException.SelfApprovalForbidden();
            }

            // §12.4 — single source of truth for the lifecycle. Replaces the
            // previous ad-hoc checks so every handler enforces the same matrix.
            ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Approved);

            // §12.1 — the project's required documents gate approval too, not only
            // submission: a document deleted after submission (or a file submitted
            // before the project declared the requirement) must not be approved.
            await _documents.EnsureSubmittableAsync(reservation.Id, reservation.UnitId, ct);

            // A buyer without a login is legitimate (§1.1): the agent records a
            // walk-in on the reservation's own identity fields and no account
            // ever exists. Approval used to refuse those outright — and did it by
            // throwing FluentValidation's exception, which this API's filter does
            // not map, so it surfaced as a bare 500 INTERNAL_ERROR.
            //
            // Approval no longer depends on an account. What an account gates is
            // narrower: the legacy Purchase row (keyed on a user) and the BUYER
            // role, both skipped below when there is none.
            var hasBuyerAccount = !string.IsNullOrWhiteSpace(reservation.BuyerId);
            if (!hasBuyerAccount)
            {
                _logger.LogInformation(
                    "[ApproveReservation] Reservation {ReservationId} has no linked account; approving on its own identity fields (§1.1). Purchase row and BUYER role skipped.",
                    reservation.Id);
            }

            // §1.1 — approval is what turns a prospect into a buyer, and it does
            // so on the PERSON, not the account: a walk-in with no login is just
            // as much a buyer. The BUYER *role* is separate and only granted when
            // an account exists (see GrantBuyerRoleAsync).
            if (reservation.PrimaryContactId is not null)
            {
                var contact = await _db.CrmContacts
                    .FirstOrDefaultAsync(c => c.Id == reservation.PrimaryContactId, ct);
                if (contact is not null && contact.LifecycleStatus == ContactLifecycleStatus.Prospect)
                {
                    contact.LifecycleStatus = ContactLifecycleStatus.Buyer;
                    contact.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "[ApproveReservation] Contact {ContactNumber} promoted PROSPECT -> BUYER.",
                        contact.ContactNumber);
                }
            }

            // Mark as approved
            _logger.LogInformation("[ApproveReservation] Marking reservation as approved...");
            reservation.Status = ReservationStatus.Approved;
            reservation.ValidatedAt = DateTime.UtcNow;
            // Record who actually approved, from the token — not the client-supplied id.
            reservation.ValidatedBy = callerId ?? request.AdminUserId;
            reservation.AdminNote = request.AdminNote;

            // Attach any uploaded documents - using direct insert to avoid navigation property issues
            var documentsToAdd = new List<ReservationDocument>();
            if (request.Documents?.Any() == true)
            {
                _logger.LogInformation("[ApproveReservation] Preparing {Count} documents for insertion...", request.Documents.Count);
                foreach (var d in request.Documents)
                {
                    _logger.LogInformation("[ApproveReservation] Creating document: FileName={FileName}, Url={Url}, ContentType={ContentType}, SizeBytes={SizeBytes}, DocumentType={DocumentType}",
                        d.FileName, d.Url, d.ContentType, d.SizeBytes, d.DocumentType);
                    
                    var doc = new ReservationDocument
                    {
                        Id = Guid.NewGuid(),
                        ReservationId = reservation.Id,
                        FileName = d.FileName,
                        Url = d.Url,
                        ContentType = d.ContentType,
                        SizeBytes = d.SizeBytes,
                        DocumentType = d.DocumentType,
                        UploadedAt = DateTime.UtcNow,
                        UploadedBy = request.AdminUserId
                    };
                    documentsToAdd.Add(doc);
                    _logger.LogInformation("[ApproveReservation] Document prepared with Id={DocId}", doc.Id);
                }
            }

            // §5.3 "Approve ⇒ unit RESERVED" — the unit hold is promoted in the
            // same transaction as the reservation. Before this, approval left the
            // unit sitting at its old status entirely.
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            await _unitStatus.TransitionAsync(
                reservation.UnitId,
                UnitCommercialStatus.Reserved,
                UnitStatusCause.ReservationApproved,
                reservationId: reservation.Id,
                actorUserId: callerId ?? request.AdminUserId,
                ct: ct);

            // Idempotency: check if a Purchase already exists for this reservation.
            // Purchase.UserId is non-nullable, so this legacy row only exists for
            // a buyer who has an account. (§6.3 flags Purchase/Sale as a shadow of
            // the reservation + payment ledger, to be retired.)
            _logger.LogInformation("[ApproveReservation] Checking for existing purchase...");
            var existing = hasBuyerAccount
                ? await _purchaseRepo.Find(p => p.ReservationId == reservation.Id)
                : Array.Empty<Purchase>();
            if (hasBuyerAccount && !existing.Any())
            {
                _logger.LogInformation("[ApproveReservation] No existing purchase, creating new one...");
                var purchase = new Purchase
                {
                    Id = Guid.NewGuid(),
                    UserId = reservation.BuyerId!,
                    ReservationId = reservation.Id,
                    TotalPrice = reservation.TotalPropertyPrice,
                    PaidAmount = reservation.ReservationAmount, // deposit already paid
                    RemainingAmount = reservation.TotalPropertyPrice - reservation.ReservationAmount,
                    CreatedAt = DateTime.UtcNow
                };

                await _purchaseRepo.InsertAsync(purchase);
                _logger.LogInformation("[ApproveReservation] Purchase created with Id={PurchaseId}", purchase.Id);
            }
            else
            {
                _logger.LogInformation("[ApproveReservation] Purchase already exists, skipping creation");
            }

            // Persist reservation changes first
            _logger.LogInformation("[ApproveReservation] Updating reservation...");
            await _reservationRepo.Update(reservation);
            
            _logger.LogInformation("[ApproveReservation] Saving reservation changes...");
            await _reservationRepo.SaveAsync();
            
            // Now add documents directly via repository (after reservation is saved)
            if (documentsToAdd.Any())
            {
                _logger.LogInformation("[ApproveReservation] Adding {Count} documents to database...", documentsToAdd.Count);
                foreach (var doc in documentsToAdd)
                {
                    await _reservationRepo.AddDocumentAsync(doc);
                    _logger.LogInformation("[ApproveReservation] Document {DocId} added to context", doc.Id);
                }
                _logger.LogInformation("[ApproveReservation] Saving documents...");
                await _reservationRepo.SaveAsync();
                _logger.LogInformation("[ApproveReservation] Documents saved successfully");
            }
            
            _logger.LogInformation("[ApproveReservation] Saving purchase changes...");
            await _purchaseRepo.SaveAsync();

            await transaction.CommitAsync(ct);

            // Granted after the commit, deliberately. The role is a key to the
            // buyer portal, not part of the sale, so it must not share the
            // transaction — UserManager runs on the same scoped DbContext, and
            // issuing it mid-transaction raised "A second operation was started
            // on this context instance". Approval is already durable here; a
            // failure to grant is logged, never fatal.
            if (hasBuyerAccount)
            {
                await GrantBuyerRoleAsync(reservation.BuyerId!, reservation.Id);
            }
            else if (reservation.PrimaryContactId is not null)
            {
                // §1.1/§6.2 — "if the approved prospect has no account yet, the
                // system should send an invitation to activate one; it must
                // not silently create a password or duplicate CRM contact."
                // Same non-fatal treatment as the role grant above: approval
                // already committed, a failed invite is logged, not thrown.
                await IssueInvitationAsync(reservation.PrimaryContactId.Value, reservation.Id, ct);
            }

            // §6.2 — buyer/agent notification, same non-fatal treatment as the
            // buyer-role grant above: the approval itself already succeeded.
            if (hasBuyerAccount)
            {
                await _notifications.NotifyAsync(
                    reservation.BuyerId!, "RESERVATION_APPROVED",
                    "Réservation approuvée",
                    "Votre réservation a été approuvée. Vous pouvez accéder à votre espace acheteur.",
                    reservation.Id, "Reservation", ct);
            }
            if (!string.IsNullOrWhiteSpace(reservation.AgentId))
            {
                await _notifications.NotifyAsync(
                    reservation.AgentId, "RESERVATION_APPROVED",
                    "Réservation approuvée",
                    $"La réservation {reservation.Id} a été approuvée.",
                    reservation.Id, "Reservation", ct);
            }

            _logger.LogInformation("[ApproveReservation] SUCCESS! Reservation {ReservationId} approved, unit {UnitId} RESERVED", request.ReservationId, reservation.UnitId);
            return true;
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "[ApproveReservation] NotFoundException: {Message}", ex.Message);
            throw;
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "[ApproveReservation] ValidationException: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ApproveReservation] UNEXPECTED ERROR: {Message}. StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            throw;
        }
    }

    /// <summary>
    /// §6.2 — "le rôle BUYER est attribué automatiquement au compte lié lorsqu'une
    /// première réservation est approuvée". Approval is the only thing that makes
    /// a buyer; the role is never self-selected at signup (see AuthenticationAPI
    /// <c>RegisterValidator</c>, which excludes it for that reason).
    ///
    /// Two deliberate choices here:
    ///
    /// * <b>Idempotent.</b> Re-approving, or a buyer with several files, must not
    ///   produce a duplicate role assignment.
    /// * <b>Non-fatal.</b> A failure to grant is logged, not thrown. The role is a
    ///   key to the buyer portal, not part of the sale: the reservation really was
    ///   approved, the unit really is reserved, and rolling that back because a
    ///   portal permission could not be written would be the worse outcome. The
    ///   warning is what surfaces it.
    ///
    /// Note this grants the role on the account only. Being a buyer is a fact
    /// about the *person*, which belongs on `crm_contacts.lifecycle_status` — a
    /// table that does not exist yet (audit B10). Until it does, a buyer with no
    /// account is recorded only as loose text on the reservation.
    /// </summary>
    private async Task GrantBuyerRoleAsync(string buyerUserId, Guid reservationId)
    {
        try
        {
            var buyer = await _userManager.FindByIdAsync(buyerUserId);
            if (buyer is null)
            {
                _logger.LogWarning(
                    "[ApproveReservation] BuyerId {BuyerId} on reservation {ReservationId} matches no account; BUYER role not granted.",
                    buyerUserId, reservationId);
                return;
            }

            if (await _userManager.IsInRoleAsync(buyer, RoleCodes.Buyer))
            {
                return;
            }

            var result = await _userManager.AddToRoleAsync(buyer, RoleCodes.Buyer);
            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "[ApproveReservation] Granted BUYER to {BuyerId} following approval of {ReservationId}.",
                    buyerUserId, reservationId);
            }
            else
            {
                _logger.LogWarning(
                    "[ApproveReservation] Could not grant BUYER to {BuyerId}: {Errors}",
                    buyerUserId, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[ApproveReservation] Granting BUYER to {BuyerId} failed; the approval itself stands.",
                buyerUserId);
        }
    }

    /// <summary>
    /// §1.1/§6.2 — an approved buyer with no account gets invited to activate
    /// one, never a silently-created password and never a second CrmContact:
    /// AccountInvitationService resolves the token against the SAME contact
    /// ContactResolver already found for this reservation.
    /// </summary>
    private async Task IssueInvitationAsync(Guid crmContactId, Guid reservationId, CancellationToken ct)
    {
        try
        {
            var invitation = await _accountInvitations.IssueAsync(crmContactId, ct);
            if (invitation is null)
            {
                _logger.LogInformation(
                    "[ApproveReservation] Contact {ContactId} on reservation {ReservationId} has no email; no invitation to send.",
                    crmContactId, reservationId);
                return;
            }

            _logger.LogInformation(
                "[ApproveReservation] Invitation issued to {Email} (contact {ContactId}) following approval of {ReservationId}.",
                invitation.Email, crmContactId, reservationId);

            // Sending the actual email is outside this handler's concern
            // (no email provider is wired into ProjectAPI today); the token
            // is durable and can be dispatched by whatever channel is added.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[ApproveReservation] Issuing an invitation for contact {ContactId} failed; the approval itself stands.",
                crmContactId);
        }
    }
}
