using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Application.Sales.AfterSales.RespondToClaimResolution;

public class RespondToClaimResolutionHandler : IRequestHandler<RespondToClaimResolutionCommand, bool>
{
    private readonly IAfterSaleClaimRepository _claimRepo;
    private readonly IClaimHistoryRepository _historyRepo;
    private readonly ICurrentUser _currentUser;

    public RespondToClaimResolutionHandler(
        IAfterSaleClaimRepository claimRepo,
        IClaimHistoryRepository historyRepo,
        ICurrentUser currentUser)
    {
        _claimRepo = claimRepo;
        _historyRepo = historyRepo;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(RespondToClaimResolutionCommand r, CancellationToken ct)
    {
        var claim = await _claimRepo.GetByIDAsync(r.ClaimId)
            ?? throw new NotFoundException($"Claim {r.ClaimId} not found.");

        // A buyer only ever acts on their own claim — never staff's project
        // perimeter, since this route isn't reachable by staff at all.
        if (string.IsNullOrEmpty(claim.BuyerId) || claim.BuyerId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Cette réclamation n'est pas accessible.",
                StatusCodes.Status403Forbidden);
        }

        if (claim.Status != ClaimStatus.Resolved)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Seule une réclamation résolue peut être confirmée ou rouverte (statut actuel : {claim.Status}).",
                StatusCodes.Status409Conflict);
        }

        var targetStatus = r.Accept ? ClaimStatus.Closed : ClaimStatus.InProgress;

        // Same matrix the staff-only route enforces (§20/§47.5) — a buyer
        // gets no wider set of transitions, just these two, reached the
        // same legal way.
        ClaimStateMachine.EnsureCanTransition(claim.Status, targetStatus);

        if (!r.Accept && string.IsNullOrWhiteSpace(r.Reason))
        {
            throw new ValidationException("A reason is required to reopen a resolved claim.");
        }

        var from = claim.Status;
        claim.Status = targetStatus;
        claim.UpdatedAt = DateTime.UtcNow;

        if (targetStatus == ClaimStatus.Closed)
        {
            claim.ClosedAt = DateTime.UtcNow;
        }
        else
        {
            // Same "reopen" bookkeeping as the staff-driven path (§20).
            claim.ReopenCount++;
            claim.ResolvedAt = null;
            claim.ResolutionSummary = null;
        }

        await _historyRepo.InsertAsync(new ClaimHistory
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FromStatus = from,
            ToStatus = claim.Status,
            ChangedByUserId = _currentUser.UserId,
            Note = r.Reason
        });

        await _claimRepo.Update(claim);
        await _historyRepo.SaveAsync();
        await _claimRepo.SaveAsync();

        return true;
    }
}
