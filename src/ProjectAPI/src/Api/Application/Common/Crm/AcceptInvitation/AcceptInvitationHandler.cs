using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Internal.ProvisionInternalUser;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Crm.AcceptInvitation;

/// <summary>
/// N20 — the account-creation half of the invitation flow that
/// ApproveReservationHandler's IssueInvitationAsync always assumed would
/// exist. This module owns the invitation and the CrmContact link; the
/// identity module owns the account and remains the only thing that ever
/// calls UserManager.CreateAsync (§6.1), which is why the account is created
/// by sending ProvisionInternalUserCommand rather than inline here.
/// </summary>
public class AcceptInvitationHandler : IRequestHandler<AcceptInvitationCommand, AcceptInvitationResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ISender _sender;

    public AcceptInvitationHandler(ApplicationDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
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

        var provisioned = await _sender.Send(new ProvisionInternalUserCommand
        {
            Email = invitation.Email,
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Password = request.Password,
            PhoneNumber = contact.Phone,
            RoleCode = RoleCodes.Buyer
        }, ct);

        // §1.1 — one account maps to at most one contact (filtered unique
        // index). Provisioning is idempotent (an existing account for this
        // email gains the role rather than erroring), so a contact already
        // linked to a DIFFERENT account here would be a genuine data
        // inconsistency worth refusing rather than silently overwriting.
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
