namespace ProjectAPI.Domain.Crm.Entities;

/// <summary>
/// A person the business knows — spec §1.1, the model's foundational rule.
///
/// A contact is NOT an account. A visitor who asks for an appointment becomes a
/// contact and may never receive a password; an agent records a walk-in buyer the
/// same way. Before this table existed, that person had nowhere to live, so their
/// identity was copied inline onto the reservation, the appointment, the claim and
/// the sale — four unlinked copies of the same human that the system could not
/// recognise as one.
///
/// The link to an account is deliberately INVERTED relative to the spec's wording.
/// §1.1 describes <c>users.crm_contact_id</c>, but accounts live in
/// AuthenticationAPI and contacts are business data owned here; a foreign key from
/// the auth service into the business domain points the dependency the wrong way.
/// Storing <see cref="UserId"/> here preserves the same guarantee — one account
/// maps to at most one contact, enforced by a filtered unique index — while
/// keeping AuthenticationAPI ignorant of the CRM. There is no cross-database FK.
///
/// One person keeps ONE contact for life. The lifecycle advances
/// PROSPECT → BUYER → DELIVERED_OWNER on the same row; a status change never
/// creates a second contact.
/// </summary>
public class CrmContact
{
    public Guid Id { get; set; }

    /// <summary>Readable business key (CT-000123), unique, generated on create.</summary>
    public string ContactNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>National identity number, when collected.</summary>
    public string? Cin { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// Upper-cased, trimmed email used for matching. Indexed but NOT unique: a
    /// contact may legitimately share none, and uniqueness of a verified email
    /// belongs to the account, not the person.
    /// </summary>
    public string? EmailNormalized { get; set; }

    public string? Phone { get; set; }

    /// <summary>
    /// Digits-only phone used for matching. Explicitly NOT unique (§1.1): a
    /// household shares a number, so a phone match raises a duplicate candidate
    /// for a human to judge — never an automatic merge (§9).
    /// </summary>
    public string? PhoneNormalized { get; set; }

    /// <summary>"fr" or "en" (§5.5). Drives the language of what we send them.</summary>
    public string PreferredLanguage { get; set; } = "fr";

    public ContactLifecycleStatus LifecycleStatus { get; set; } = ContactLifecycleStatus.Prospect;

    /// <summary>Where the contact came from — referential code, free for now.</summary>
    public string? AcquisitionSourceCode { get; set; }

    /// <summary>Sales agent who owns the relationship, when one is assigned.</summary>
    public string? OwnerSalesAgentId { get; set; }

    /// <summary>
    /// The account this person signed up with, if they ever did. Null is the
    /// normal case for a walk-in. Unique among non-null values.
    /// </summary>
    public string? UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Contacts are archived, never deleted (§9).</summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>Normalises an email for matching. Null stays null.</summary>
    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToUpperInvariant();

    /// <summary>
    /// Reduces a phone number to its digits so "+212 661-000111", "0661000111"
    /// and "0661 00 01 11" all match. Leading country code is not stripped —
    /// that would need a locale rule this model does not yet carry.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}

/// <summary>
/// §1.1 lifecycle. Stored as a string (§6.1: statuses are varchar + CHECK, never
/// a database enum) via the codes below.
/// </summary>
public enum ContactLifecycleStatus
{
    /// <summary>Known to the CRM, no approved reservation.</summary>
    Prospect = 0,

    /// <summary>Has at least one approved reservation.</summary>
    Buyer = 1,

    /// <summary>Has taken delivery of at least one unit.</summary>
    DeliveredOwner = 2,

    /// <summary>Kept for history, excluded from active work.</summary>
    Archived = 3,
}

/// <summary>Canonical §1.1 codes for <see cref="ContactLifecycleStatus"/>.</summary>
public static class ContactLifecycleCodes
{
    public const string Prospect = "PROSPECT";
    public const string Buyer = "BUYER";
    public const string DeliveredOwner = "DELIVERED_OWNER";
    public const string Archived = "ARCHIVED";

    public static readonly string[] All = { Prospect, Buyer, DeliveredOwner, Archived };

    public static string ToCode(this ContactLifecycleStatus status) => status switch
    {
        ContactLifecycleStatus.Prospect => Prospect,
        ContactLifecycleStatus.Buyer => Buyer,
        ContactLifecycleStatus.DeliveredOwner => DeliveredOwner,
        ContactLifecycleStatus.Archived => Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown contact lifecycle."),
    };

    public static ContactLifecycleStatus Parse(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            Prospect => ContactLifecycleStatus.Prospect,
            Buyer => ContactLifecycleStatus.Buyer,
            DeliveredOwner => ContactLifecycleStatus.DeliveredOwner,
            Archived => ContactLifecycleStatus.Archived,
            _ => ContactLifecycleStatus.Prospect,
        };
}
