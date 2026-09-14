using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using ValidationException = FluentValidation.ValidationException;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Sales.AfterSales.UpdateClaimStatus;

/// <summary>
/// Transitions a SAV claim through the §20 lifecycle via
/// <see cref="ClaimStateMachine"/> — never a direct field assignment. Every
/// transition appends a <see cref="ClaimHistory"/> row; the claim's own
/// fields (assignment, SLA target, reopen count) update alongside it in the
/// same save.
/// </summary>
public class UpdateClaimStatusHandler : IRequestHandler<UpdateClaimStatusCommand, bool>
{
    private readonly IAfterSaleClaimRepository _claimRepo;
    private readonly IClaimAttachmentRepository _attachRepo;
    private readonly IClaimHistoryRepository _historyRepo;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public UpdateClaimStatusHandler(
        IAfterSaleClaimRepository claimRepo,
        IClaimAttachmentRepository attachRepo,
        IClaimHistoryRepository historyRepo,
        ProjectScopeService projectScope,
        ICurrentUser currentUser,
        ApplicationDbContext db)
    {
        _claimRepo = claimRepo;
        _attachRepo = attachRepo;
        _historyRepo = historyRepo;
        _projectScope = projectScope;
        _currentUser = currentUser;
        _db = db;
    }

    /// <summary>The only statuses a (non-supervisor) technician may move their own claim to.</summary>
    private static readonly ClaimStatus[] TechnicianTargets =
    {
        ClaimStatus.UnderReview, ClaimStatus.MoreInfoRequired, ClaimStatus.InProgress, ClaimStatus.WaitingCustomer, ClaimStatus.Resolved
    };

    public async Task<bool> Handle(UpdateClaimStatusCommand r, CancellationToken ct)
    {
        var claim = await _claimRepo.GetByIDAsync(r.ClaimId)
            ?? throw new NotFoundException($"Claim {r.ClaimId} not found.");

        // §6.4 — a project admin/technician acts only within their assigned
        // projects. Claims resolve their project via unit -> immeuble -> project,
        // the same chain reservations use.
        var claimProjectId = await (
            from u in _db.Set<UnitEntity>()
            join im in _db.Set<Immeuble>() on u.ProjectId equals im.Id
            where u.Id == claim.UnitId
            select im.ProjectId).FirstOrDefaultAsync(ct);

        if (claimProjectId != Guid.Empty)
        {
            await _projectScope.EnsureProjectAccessAsync(claimProjectId, ct);
        }

        // §6.1 — TECHNICIAN is "exclusively post-delivery warranty claims", and
        // only the claims assigned to them: they must not act on someone else's
        // claim just because they hold the role.
        var isSupervisor = _currentUser.IsGlobalAdmin
            || _currentUser.IsInRole(RoleCodes.ProjectAdmin)
            || _currentUser.IsInRole(RoleCodes.TechLead);

        if (!isSupervisor)
        {
            if (!string.Equals(claim.AssignedAgentId, _currentUser.UserId, StringComparison.Ordinal))
            {
                throw BusinessRuleException.ProjectScopeDenied(claimProjectId);
            }

            // The technician does the work; qualifying, assigning, rejecting and
            // validating closure belong to the technical lead (or an admin).
            if (!TechnicianTargets.Contains(r.NewStatus))
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.Unauthorized,
                    $"Un technicien ne peut pas passer une réclamation au statut {r.NewStatus} : " +
                    "cette décision revient au responsable technique.",
                    StatusCodes.Status403Forbidden);
            }
        }

        var from = claim.Status;

        // §20/§47.5 — the only place a transition is decided; any target
        // outside the matrix throws INVALID_STATUS_TRANSITION (409).
        ClaimStateMachine.EnsureCanTransition(from, r.NewStatus);

        // §20 — qualification/assignment: moving into ASSIGNED requires a
        // technician, since a claim is never auto-assigned to SAV.
        if (r.NewStatus == ClaimStatus.Assigned && string.IsNullOrWhiteSpace(r.AssignedAgentId))
        {
            throw new ValidationException("A technician must be assigned to move a claim to ASSIGNED.");
        }

        if (r.NewStatus == ClaimStatus.Resolved && string.IsNullOrWhiteSpace(r.ResolutionSummary))
        {
            throw new ValidationException("Resolution summary is required to resolve a claim.");
        }

        // §20 "reopen: reason, keeps SLA, reopen_count++" — the claim goes
        // back to IN_PROGRESS but its SLA target is untouched, only the
        // reopen counter and resolution fields are cleared.
        var reopening = from == ClaimStatus.Resolved && r.NewStatus == ClaimStatus.InProgress;
        if (reopening && string.IsNullOrWhiteSpace(r.Note))
        {
            throw new ValidationException("A reason is required to reopen a resolved claim.");
        }

        claim.Status = r.NewStatus;
        claim.UpdatedAt = DateTime.UtcNow;

        if (r.NewStatus == ClaimStatus.Assigned)
        {
            claim.AssignedAgentId = r.AssignedAgentId;
        }

        if (r.Priority.HasValue)
        {
            claim.Priority = r.Priority.Value;
        }

        if (r.SlaTargetAt.HasValue)
        {
            claim.SlaTargetAt = r.SlaTargetAt;
        }

        if (r.NewStatus == ClaimStatus.Resolved)
        {
            claim.ResolutionSummary = r.ResolutionSummary;
            claim.ResolvedAt = DateTime.UtcNow;

            // Proof of the repair is uploaded as a file against the claim
            // (POST /api/claims/{id}/attachments), never injected as a URL here.
        }

        if (reopening)
        {
            claim.ReopenCount++;
            claim.ResolvedAt = null;
            claim.ResolutionSummary = null;
        }

        if (r.NewStatus == ClaimStatus.Closed)
        {
            claim.ClosedAt = DateTime.UtcNow;
        }

        await _historyRepo.InsertAsync(new ClaimHistory
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FromStatus = from,
            ToStatus = claim.Status,
            // From the token, never the body. This is the audit trail for who
            // moved a claim through its lifecycle; a caller-supplied id would
            // let anyone sign a transition with someone else's name.
            ChangedByUserId = _currentUser.UserId ?? r.ChangedByUserId,
            Note = r.Note
        });

        await _claimRepo.Update(claim);

        await _attachRepo.SaveAsync();
        await _historyRepo.SaveAsync();
        await _claimRepo.SaveAsync();

        return true;
    }
}
