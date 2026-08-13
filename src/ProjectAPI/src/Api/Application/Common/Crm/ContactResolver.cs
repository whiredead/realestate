using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Crm;

public interface IContactResolver
{
    /// <summary>
    /// Finds the person these details belong to, or creates them. Never returns
    /// null and never creates a second contact for someone already known.
    /// </summary>
    Task<CrmContact> ResolveAsync(
        string? firstName,
        string? lastName,
        string? email,
        string? phone,
        string? cin = null,
        string? userId = null,
        CancellationToken ct = default);
}

/// <summary>
/// Turns the identity typed on a form into the one <see cref="CrmContact"/> that
/// represents that person (§1.1).
///
/// Matching is deliberately asymmetric:
///
/// * <b>Email is decisive.</b> A verified address identifies one person, so a
///   match reuses the existing contact.
/// * <b>Phone is not.</b> §1.1 says a household shares a number, so a phone-only
///   match is recorded as a duplicate candidate and a NEW contact is created.
///   Fusing two people because they share a landline is exactly the automatic
///   merge §9 forbids — the decision belongs to a human.
/// * <b>An account id is decisive</b> when present, since one account maps to at
///   most one contact.
/// </summary>
public class ContactResolver : IContactResolver
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ContactResolver> _logger;

    public ContactResolver(ApplicationDbContext db, ILogger<ContactResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CrmContact> ResolveAsync(
        string? firstName,
        string? lastName,
        string? email,
        string? phone,
        string? cin = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        var emailNorm = CrmContact.NormalizeEmail(email);
        var phoneNorm = CrmContact.NormalizePhone(phone);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var byUser = await _db.CrmContacts.FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (byUser is not null) return byUser;
        }

        if (emailNorm is not null)
        {
            var byEmail = await _db.CrmContacts
                .FirstOrDefaultAsync(c => c.EmailNormalized == emailNorm, ct);
            if (byEmail is not null)
            {
                // The person is already known; enrich rather than duplicate (§1.1).
                if (byEmail.UserId is null && !string.IsNullOrWhiteSpace(userId)) byEmail.UserId = userId;
                if (byEmail.PhoneNormalized is null && phoneNorm is not null)
                {
                    byEmail.Phone = phone;
                    byEmail.PhoneNormalized = phoneNorm;
                }
                if (string.IsNullOrWhiteSpace(byEmail.Cin) && !string.IsNullOrWhiteSpace(cin)) byEmail.Cin = cin;
                byEmail.UpdatedAt = DateTime.UtcNow;
                return byEmail;
            }
        }

        if (phoneNorm is not null)
        {
            var sharesPhone = await _db.CrmContacts
                .Where(c => c.PhoneNormalized == phoneNorm)
                .Select(c => c.ContactNumber)
                .ToListAsync(ct);

            if (sharesPhone.Count > 0)
            {
                // Alert, never merge (§1.1/§9). A dedicated duplicate_candidates
                // table is the fuller implementation; until it exists this is the
                // audit trail that the ambiguity was seen and not silently resolved.
                _logger.LogWarning(
                    "[Crm] Phone {Phone} already belongs to contact(s) {Existing}; creating a separate contact for {First} {Last}. Phone is not unique (§1.1) — review as a possible duplicate.",
                    phoneNorm, string.Join(", ", sharesPhone), firstName, lastName);
            }
        }

        var contact = new CrmContact
        {
            Id = Guid.NewGuid(),
            ContactNumber = await NextContactNumberAsync(ct),
            FirstName = string.IsNullOrWhiteSpace(firstName) ? "(inconnu)" : firstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(lastName) ? "(inconnu)" : lastName.Trim(),
            Cin = string.IsNullOrWhiteSpace(cin) ? null : cin.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            EmailNormalized = emailNorm,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            PhoneNormalized = phoneNorm,
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            LifecycleStatus = ContactLifecycleStatus.Prospect,
            CreatedAt = DateTime.UtcNow,
        };

        _db.CrmContacts.Add(contact);
        return contact;
    }

    /// <summary>
    /// N5 — a reservation with co-buyers resolves several NEW contacts in the
    /// same unsaved DbContext before a single SaveChanges. CountAsync() only
    /// sees persisted rows, so the primary buyer and each co-buyer all
    /// computed the same "next" number and collided on ContactNumber's
    /// unique index at save time (409). Local (Added-but-unsaved) entries
    /// must count too, since they occupy numbers this same call is about to
    /// hand out again.
    /// </summary>
    private async Task<string> NextContactNumberAsync(CancellationToken ct)
    {
        var persistedCount = await _db.CrmContacts.CountAsync(ct);
        var pendingCount = _db.ChangeTracker.Entries<CrmContact>()
            .Count(e => e.State == EntityState.Added);
        return $"CT-{persistedCount + pendingCount + 1:D6}";
    }
}
