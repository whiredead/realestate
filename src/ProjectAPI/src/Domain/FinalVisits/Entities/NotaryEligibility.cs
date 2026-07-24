namespace ProjectAPI.Domain.FinalVisits.Entities;

/// <summary>
/// Notary-appointment eligibility (spec §17.6 FR-FVI-012).
/// </summary>
public enum NotaryEligibilityStatus
{
    Eligible = 0,
    EligibleWithMinorSnags = 1,
    NotEligibleMajorSnags = 2,
    BlockedBlockingSnags = 3,
    NotEligibleFinalVisitPending = 4
}

/// <summary>Eligibility result plus the reasons behind it (§17.6).</summary>
public class NotaryEligibilityResult
{
    public NotaryEligibilityStatus Status { get; set; }

    /// <summary>True when a notary appointment may be requested.</summary>
    public bool CanRequestAppointment =>
        Status is NotaryEligibilityStatus.Eligible or NotaryEligibilityStatus.EligibleWithMinorSnags;

    /// <summary>Human-readable causes, returned to the client (§17.6).</summary>
    public List<string> Reasons { get; set; } = new();

    public int ActiveMinorSnags { get; set; }
    public int ActiveMajorSnags { get; set; }
    public int ActiveBlockingSnags { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Computes notary eligibility from the final-visit dossier (spec §17.6).
///
/// The spec fixes the priority order (§17.6 FR-FVI-013):
///   1. visit not completed / report not acknowledged → NOT_ELIGIBLE
///   2. any active BLOCKING snag                      → BLOCKED
///   3. any active MAJOR snag                         → NOT_ELIGIBLE
///   4. only active MINOR snags                       → ELIGIBLE_WITH_MINOR_SNAGS
///   5. no active snag                                → ELIGIBLE
///
/// This lives in the domain because §17.6 requires the API to return the result:
/// the frontend must never recompute it.
/// </summary>
public static class NotaryEligibilityCalculator
{
    public static NotaryEligibilityResult Calculate(
        FinalVisitCase? visitCase,
        FinalVisitReport? currentReport,
        IEnumerable<Snag> snags)
    {
        var result = new NotaryEligibilityResult();

        // --- 1. the visit itself must be done and acknowledged ---
        if (visitCase is null)
        {
            result.Status = NotaryEligibilityStatus.NotEligibleFinalVisitPending;
            result.Reasons.Add("Aucune visite finale n'a été demandée.");
            return result;
        }

        if (currentReport is null)
        {
            result.Status = NotaryEligibilityStatus.NotEligibleFinalVisitPending;
            result.Reasons.Add("La visite finale n'a pas encore fait l'objet d'un compte rendu.");
            return result;
        }

        if (currentReport.Status != ReportStatus.Acknowledged)
        {
            result.Status = NotaryEligibilityStatus.NotEligibleFinalVisitPending;
            result.Reasons.Add(
                currentReport.Status == ReportStatus.Disputed
                    ? "Le compte rendu de visite finale est contesté par l'acheteur."
                    : "Le compte rendu de visite finale n'a pas encore été validé par l'acheteur.");
            return result;
        }

        // --- 2..5. weigh the ACTIVE snags ---
        // "Active" means not yet VALIDATED or CLOSED: a major snag merely marked
        // RESOLVED still blocks until it has been validated (§17.4).
        var active = snags.Where(s => SnagStateMachine.IsActive(s.Status)).ToList();

        result.ActiveBlockingSnags = active.Count(s => s.Severity == SnagSeverity.Blocking);
        result.ActiveMajorSnags = active.Count(s => s.Severity == SnagSeverity.Major);
        result.ActiveMinorSnags = active.Count(s => s.Severity == SnagSeverity.Minor);

        if (result.ActiveBlockingSnags > 0)
        {
            result.Status = NotaryEligibilityStatus.BlockedBlockingSnags;
            result.Reasons.Add($"{result.ActiveBlockingSnags} réserve(s) bloquante(s) non levée(s).");
            return result;
        }

        if (result.ActiveMajorSnags > 0)
        {
            result.Status = NotaryEligibilityStatus.NotEligibleMajorSnags;
            result.Reasons.Add($"{result.ActiveMajorSnags} réserve(s) majeure(s) en attente de résolution et de validation.");
            return result;
        }

        if (result.ActiveMinorSnags > 0)
        {
            result.Status = NotaryEligibilityStatus.EligibleWithMinorSnags;
            result.Reasons.Add($"{result.ActiveMinorSnags} réserve(s) mineure(s) restante(s) — le rendez-vous notarial reste autorisé.");
            return result;
        }

        result.Status = NotaryEligibilityStatus.Eligible;
        result.Reasons.Add("Aucune réserve active.");
        return result;
    }
}
