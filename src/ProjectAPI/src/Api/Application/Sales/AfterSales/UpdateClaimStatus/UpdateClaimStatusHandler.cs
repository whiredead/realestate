using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Application.Sales.AfterSales.UpdateClaimStatus;

public class UpdateClaimStatusHandler : IRequestHandler<UpdateClaimStatusCommand, bool>
{
    private readonly IAfterSaleClaimRepository _claimRepo;
    private readonly IClaimAttachmentRepository _attachRepo;
    private readonly IClaimHistoryRepository _historyRepo;

    public UpdateClaimStatusHandler(
        IAfterSaleClaimRepository claimRepo,
        IClaimAttachmentRepository attachRepo,
        IClaimHistoryRepository historyRepo)
    {
        _claimRepo = claimRepo;
        _attachRepo = attachRepo;
        _historyRepo = historyRepo;
    }

    public async Task<bool> Handle(UpdateClaimStatusCommand r, CancellationToken ct)
    {
        var claim = await _claimRepo.GetByIDAsync(r.ClaimId)
            ?? throw new NotFoundException($"Claim {r.ClaimId} not found.");

        var from = claim.Status;
        claim.Status = r.NewStatus;
        claim.UpdatedAt = DateTime.UtcNow;

        if (r.NewStatus == ClaimStatus.Resolved)
        {
            if (string.IsNullOrWhiteSpace(r.ResolutionSummary))
                throw new ValidationException("Resolution summary is required to resolve a claim.");

            claim.ResolutionSummary = r.ResolutionSummary;
            claim.ResolvedAt = DateTime.UtcNow;

            foreach (var p in r.Proofs ?? [])
            {
                await _attachRepo.InsertAsync(new ClaimAttachment
                {
                    Id = Guid.NewGuid(),
                    ClaimId = claim.Id,
                    Url = p.Url,
                    FileName = p.FileName,
                    ContentType = p.ContentType,
                    SizeBytes = p.SizeBytes
                });
            }
        }

        await _historyRepo.InsertAsync(new ClaimHistory
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FromStatus = from,
            ToStatus = claim.Status,
            ChangedByUserId = r.ChangedByUserId,
            Note = r.Note
        });

        _claimRepo.Update(claim);

        await _attachRepo.SaveAsync();
        await _historyRepo.SaveAsync();
        await _claimRepo.SaveAsync();

        return true;
    }
}
