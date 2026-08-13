using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Infrastructure.Clients;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Crm.AcceptInvitation;

/// <summary>
/// N20 — the account-creation half of the Phase 2 invitation flow that
/// ApproveReservationHandler's IssueInvitationAsync always assumed would
/// exist (see ProvisionInternalUserCommand's own doc comment, which already
/// named this class). ProjectAPI owns the invitation and the CrmContact link;
/// AuthenticationAPI owns the actual account and is the only thing that ever
/// calls UserManager.CreateAsync (§6.1).
/// </summary>
public class AcceptInvitationHandler : IRequestHandler<AcceptInvitationCommand, AcceptInvitationResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuthenticationApiClient _authApi;

    public AcceptInvitationHandler(ApplicationDbContext db, IAuthenticationApiClient authApi)
    {
        _db = db;
        _authApi = authApi;
    }

    public async Task<AcceptInvitationResponse> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed, "Le lien d'activation est invalide.");
        }

        var invitation = await _db.Set<AccountInvitation>()
            .Include(i => i.CrmContact)
            .FirstOrDefaultAsync(i => i.Token == request.Token, ct)
            ?? throw new NotFoundException("Cette invitation n'existe pas.");

        // Fail closed on every terminal state, with a distinct message per
        // cause — an accepted/expired/revoked token is a dead end for the
        // invitee, not something a retry with a different password fixes.
        if (invitation.AcceptedAt is not null)
        {
            throw new BusinessRuleException(
                "INVITATION_ALREADY_ACCEPTED", "Ce lien d'activation a déjà été utilisé.", StatusCodes.Status409Conflict);
        }
        if (invitation.RevokedAt is not null)
        {
            throw new BusinessRuleException(
                "INVITATION_REVOKED", "Ce lien d'activation n'est plus valide ; une invitation plus récente a été envoyée.", StatusCodes.Status409Conflict);
        }
        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessRuleException(
                "INVITATION_EXPIRED", "Ce lien d'activation a expiré.", StatusCodes.Status409Conflict);
        }

        var contact = invitation.CrmContact;

        var provisioned = await _authApi.ProvisionBuyerAsync(new ProvisionBuyerRequest(
            invitation.Email,
            contact.FirstName,
            contact.LastName,
            request.Password,
            contact.Phone), ct);

        // §1.1 — one account maps to at most one contact (filtered unique
        // index). ProvisionBuyerAsync is idempotent on AuthenticationAPI's
        // side (existing-account-by-email adds the role rather than erroring),
        // so a contact already linked to a DIFFERENT account here would be a
        // genuine data inconsistency worth refusing rather than silently
        // overwriting.
        if (!string.IsNullOrEmpty(contact.UserId) && contact.UserId != provisioned.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Ce contact est déjà lié à un autre compte.");
        }

        contact.UserId = provisioned.UserId;
        if (contact.LifecycleStatus == ContactLifecycleStatus.Prospect)
        {
            contact.LifecycleStatus = ContactLifecycleStatus.Buyer;
        }
        contact.UpdatedAt = DateTime.UtcNow;

        invitation.AcceptedAt = DateTime.UtcNow;

        // Every reservation already carrying this contact as its buyer, but
        // still recorded on loose identity fields (BuyerId null), is now
        // linkable — mirrors how a buyer who registers with an account
        // already tied to the same email would read as "theirs" everywhere
        // the reservation is looked up by BuyerId.
        var reservationsToLink = await _db.Reservations
            .Where(r => r.PrimaryContactId == contact.Id && string.IsNullOrEmpty(r.BuyerId))
            .ToListAsync(ct);
        foreach (var reservation in reservationsToLink)
        {
            reservation.BuyerId = provisioned.UserId;
        }

        await _db.SaveChangesAsync(ct);

        return new AcceptInvitationResponse
        {
            UserId = provisioned.UserId,
            Email = provisioned.Email,
            Message = "Compte activé. Vous pouvez maintenant vous connecter."
        };
    }
}
