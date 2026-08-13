namespace ProjectAPI.Api.Application.Sales.AfterSales.RespondToClaimResolution;

/// <summary>
/// §20/§47.5 — the buyer's own response to a resolved claim: confirm it
/// (Resolved -&gt; Closed) or reopen it (Resolved -&gt; InProgress, reason
/// required). The only status-mutating claim route before this was
/// staff-only (AdminsTechnicians); a real buyer had no way to exercise
/// either transition themselves, only staff acting "on their behalf".
/// </summary>
public class RespondToClaimResolutionCommand : IRequest<bool>
{
    public Guid ClaimId { get; set; }

    /// <summary>True = confirm resolution (-&gt; Closed). False = reopen (-&gt; InProgress).</summary>
    public bool Accept { get; set; }

    /// <summary>Required when Accept is false — why the buyer isn't satisfied with the resolution.</summary>
    public string? Reason { get; set; }
}
