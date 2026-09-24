using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Crm;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.IssueActivationLink;

/// <summary>
/// Issues (or re-issues) the link a buyer uses to set their own password and activate their account, for a
/// reservation whose buyer has no account yet. Any earlier live link for the same person is revoked, so only the
/// newest one works. The agent shares the link with the buyer.
/// </summary>
public class IssueActivationLinkCommand : IRequest<IssueActivationLinkResponse>
{
    public Guid ReservationId { get; set; }
}

public class IssueActivationLinkResponse
{
    public string ActivationToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class IssueActivationLinkHandler : IRequestHandler<IssueActivationLinkCommand, IssueActivationLinkResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly IAccountInvitationService _invitations;
    private readonly UserManager<User> _userManager;

    public IssueActivationLinkHandler(
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        IAccountInvitationService invitations,
        UserManager<User> userManager)
    {
        _db = db;
        _projectScope = projectScope;
        _invitations = invitations;
        _userManager = userManager;
    }

    public async Task<IssueActivationLinkResponse> Handle(IssueActivationLinkCommand request, CancellationToken ct)
    {
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var reservation = await _db.Reservations
            .Include(r => r.PrimaryContact)
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        var contact = reservation.PrimaryContact;
        if (contact is null || string.IsNullOrWhiteSpace(contact.Email))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Cette réservation n'a pas d'adresse e-mail acheteur : impossible de créer un lien d'activation.");
        }

        if (!string.IsNullOrWhiteSpace(reservation.BuyerId) || contact.UserId is not null
            || await _userManager.FindByEmailAsync(contact.Email) is not null)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Cet acheteur a déjà un compte : aucun lien d'activation n'est nécessaire.",
                StatusCodes.Status409Conflict);
        }

        var invitation = await _invitations.IssueAsync(contact.Id, ct)
            ?? throw new BusinessRuleException(BusinessErrorCodes.ValidationFailed, "Le lien d'activation n'a pas pu être créé.");

        return new IssueActivationLinkResponse { ActivationToken = invitation.Token, ExpiresAt = invitation.ExpiresAt };
    }
}
