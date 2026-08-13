namespace ProjectAPI.Domain.Crm.Entities;

/// <summary>
/// §1.1/§6.2 — "if the approved prospect has no account yet, the system should
/// send an invitation to activate one; it must not silently create a password
/// or duplicate CRM contact."
///
/// This row is the invitation itself: a token tied to the EXISTING CrmContact
/// (never a new one), sent to the contact's email, expiring after a fixed
/// window. Accepting it does not create the account here — it hands the
/// token to AuthenticationAPI's own registration screen, pre-filled with this
/// contact's identity, so the person still chooses their own password. This
/// service only ever proves "yes, this email was approved to register as
/// BUYER and is linked to contact X" — it never touches AspNetUsers itself
/// (ProjectAPI has no dependency on the auth database).
/// </summary>
public class AccountInvitation
{
    public Guid Id { get; set; }

    public Guid CrmContactId { get; set; }
    public CrmContact CrmContact { get; set; } = null!;

    /// <summary>Opaque, unguessable token sent in the invitation link/email.</summary>
    public string Token { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    /// <summary>Set if superseded by a newer invitation before being accepted.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsUsable(DateTime asOfUtc) =>
        AcceptedAt is null && RevokedAt is null && ExpiresAt > asOfUtc;
}
