using System.Text.RegularExpressions;

namespace ProjectAPI.Api.Application.Common.Audit;

/// <summary>
/// Which MediatR requests the audit trail records, and under which code.
///
/// Convention: every <c>*Command</c> is a business action and is audited; queries
/// are not. The sets below are the only exceptions, kept in one place so the
/// policy can be reviewed at a glance. Names, not types, so the list survives a
/// handler moving folders; <c>AuditPolicyTests</c> fails if a name no longer exists.
/// </summary>
public static partial class AuditPolicy
{
    /// <summary>Personal preferences and technical state: not business acts.</summary>
    public static readonly IReadOnlySet<string> Excluded = new HashSet<string>(StringComparer.Ordinal)
    {
        "MarkNotificationReadCommand",
        "AddLikedProjectCommand", "RemoveLikedProjectCommand", "UpdateLikedProjectCommand",
        "SubmitFeedbackCommand",
    };

    /// <summary>Mutations that do not follow the *Command naming.</summary>
    public static readonly IReadOnlySet<string> ForceAudited = new HashSet<string>(StringComparer.Ordinal)
    {
        "LockoutUserQuery", "UnlockUserQuery",
    };

    /// <summary>Authentication events: recorded with their outcome, failures included, never with field values.</summary>
    public static readonly IReadOnlySet<string> Security = new HashSet<string>(StringComparer.Ordinal)
    {
        "LoginCommand", "RegisterCommand", "ConfirmEmailCommand", "ForgotPasswordCommand", "ResetPasswordCommand",
        "AdminChangePasswordCommand", "DemandeCodeCommand", "VerifyCodeCommand", "LogoutSessionCommand", "RefreshSessionCommand",
    };

    /// <summary>
    /// Security events too frequent to keep on success (a refresh every few minutes per open tab;
    /// every sign-in). Refused attempts are still journaled with their reason.
    /// </summary>
    public static readonly IReadOnlySet<string> SecurityFailuresOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "RefreshSessionCommand", "VerifyCodeCommand", "LoginCommand",
    };

    /// <summary>
    /// Reads of a buyer's personal data that are journaled as consultations (category ACCESS):
    /// the reservation file and the documents attached to it. Recorded when staff read them,
    /// never when the buyer opens their own. Query name → action, record type, id property.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, AccessSpec> Access = new Dictionary<string, AccessSpec>(StringComparer.Ordinal)
    {
        ["GetReservationByIdQuery"] = new("VIEW_RESERVATION", "Reservation", "ReservationId"),
        ["DownloadReservationDocumentQuery"] = new("DOWNLOAD_RESERVATION_DOCUMENT", "ReservationDocument", "DocumentId"),
        ["DownloadPaymentProofQuery"] = new("DOWNLOAD_PAYMENT_PROOF", "PaymentProof", "ProofId"),
        ["DownloadClaimAttachmentQuery"] = new("DOWNLOAD_CLAIM_ATTACHMENT", "ClaimAttachment", "AttachmentId"),
        ["DownloadFinalVisitDocumentQuery"] = new("DOWNLOAD_FINAL_VISIT_DOCUMENT", "FinalVisitDocument", "DocumentId"),
    };

    public static bool IsAudited(Type request) =>
        !Excluded.Contains(request.Name)
        && (request.Name.EndsWith("Command", StringComparison.Ordinal) || ForceAudited.Contains(request.Name));

    public static bool IsSecurity(Type request) => Security.Contains(request.Name);

    /// <summary>ApproveReservationCommand → APPROVE_RESERVATION. Stable: the frontend translates these codes.</summary>
    public static string ActionFor(Type request)
    {
        var name = request.Name;
        foreach (var suffix in new[] { "Command", "Query" })
            if (name.EndsWith(suffix, StringComparison.Ordinal)) { name = name[..^suffix.Length]; break; }
        return WordBoundary().Replace(name, "_$1").ToUpperInvariant();
    }

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}

/// <summary>How a consultation is journaled: its action code, the record type and the request property holding its id.</summary>
public sealed record AccessSpec(string Action, string EntityType, string IdProperty);
