namespace ProjectAPI.Domain.Appointments.Entities;

/// <summary>
/// Result of a notary appointment — spec §5.7 / §18.
///
/// The outcome is deliberately SEPARATE from the appointment's status. Status
/// says what happened to the meeting (requested, confirmed, completed…);
/// the outcome says what the meeting decided. A COMPLETED appointment where the
/// buyer did not show and a COMPLETED appointment that finalised the purchase
/// are the same status and entirely different business events.
///
/// Only <see cref="PurchaseCompleted"/> changes anything: it converts the
/// reservation and sells the unit, atomically. Every other outcome leaves the
/// reservation and the unit exactly where they were, so the file can be retried
/// with a new appointment.
/// </summary>
public enum NotaryAppointmentOutcome
{
    /// <summary>Purchase finalised — reservation → CONVERTED, unit → SOLD (§5.7).</summary>
    PurchaseCompleted = 0,

    /// <summary>Dossier incomplete; nothing changes.</summary>
    IncompleteFile = 1,

    /// <summary>Buyer absent; nothing changes.</summary>
    BuyerAbsent = 2,

    /// <summary>Deferred to a later date; nothing changes.</summary>
    Postponed = 3,

    /// <summary>Any other reason the purchase did not complete; nothing changes.</summary>
    NotCompletedOther = 4
}

/// <summary>
/// Canonical string codes for <see cref="NotaryAppointmentOutcome"/> (§6.1:
/// statuses are varchar + CHECK, not database enums).
/// </summary>
public static class NotaryOutcomeCodes
{
    public const string PurchaseCompleted = "PURCHASE_COMPLETED";
    public const string IncompleteFile = "INCOMPLETE_FILE";
    public const string BuyerAbsent = "BUYER_ABSENT";
    public const string Postponed = "POSTPONED";
    public const string NotCompletedOther = "NOT_COMPLETED_OTHER";

    public static readonly string[] All =
    {
        PurchaseCompleted, IncompleteFile, BuyerAbsent, Postponed, NotCompletedOther
    };

    public static string ToCode(this NotaryAppointmentOutcome outcome) => outcome switch
    {
        NotaryAppointmentOutcome.PurchaseCompleted => PurchaseCompleted,
        NotaryAppointmentOutcome.IncompleteFile => IncompleteFile,
        NotaryAppointmentOutcome.BuyerAbsent => BuyerAbsent,
        NotaryAppointmentOutcome.Postponed => Postponed,
        NotaryAppointmentOutcome.NotCompletedOther => NotCompletedOther,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown notary outcome.")
    };

    public static NotaryAppointmentOutcome Parse(string value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            PurchaseCompleted => NotaryAppointmentOutcome.PurchaseCompleted,
            IncompleteFile => NotaryAppointmentOutcome.IncompleteFile,
            BuyerAbsent => NotaryAppointmentOutcome.BuyerAbsent,
            Postponed => NotaryAppointmentOutcome.Postponed,
            NotCompletedOther => NotaryAppointmentOutcome.NotCompletedOther,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown notary outcome code.")
        };

    public static bool TryParse(string? value, out NotaryAppointmentOutcome outcome)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                outcome = default;
                return false;
            }

            outcome = Parse(value);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            outcome = default;
            return false;
        }
    }
}
