using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Crm;

/// <summary>
/// §1.1/§6.2 — issues the invitation an approved-but-account-less buyer needs
/// to activate their own login. Never creates a password, never a second
/// CrmContact: the token only ever points at the contact already resolved by
/// ContactResolver.
/// </summary>
public interface IAccountInvitationService
{
    /// <summary>
    /// Issues a fresh invitation for the given contact, revoking any prior
    /// unused one first — a contact never holds two live invitations.
    /// Returns null when the contact has no email (nothing to send it to).
    /// </summary>
    Task<AccountInvitation?> IssueAsync(Guid crmContactId, CancellationToken ct);
}

public class AccountInvitationService : IAccountInvitationService
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private readonly ApplicationDbContext _db;

    public AccountInvitationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AccountInvitation?> IssueAsync(Guid crmContactId, CancellationToken ct)
    {
        var contact = await _db.CrmContacts.FirstOrDefaultAsync(c => c.Id == crmContactId, ct);
        if (contact is null || string.IsNullOrWhiteSpace(contact.Email))
        {
            return null;
        }

        // A person keeps ONE contact for life (§1.1); an invitation is
        // likewise singular per contact — revoke rather than accumulate.
        var now = DateTime.UtcNow;
        var priorLive = await _db.Set<AccountInvitation>()
            .Where(i => i.CrmContactId == crmContactId && i.AcceptedAt == null && i.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var prior in priorLive)
        {
            prior.RevokedAt = now;
        }

        var invitation = new AccountInvitation
        {
            Id = Guid.NewGuid(),
            CrmContactId = crmContactId,
            Token = GenerateToken(),
            Email = contact.Email,
            CreatedAt = now,
            ExpiresAt = now.Add(Validity)
        };

        _db.Add(invitation);
        await _db.SaveChangesAsync(ct);

        return invitation;
    }

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
