using ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Api.Application.Sales.AfterSales.UpdateClaimStatus;

public class UpdateClaimStatusCommand : IRequest<bool>
{
    public Guid ClaimId { get; set; }
    public ClaimStatus NewStatus { get; set; }
    public string ChangedByUserId { get; set; }
    public string? Note { get; set; }                 // e.g. reason or progress note
    public string? ResolutionSummary { get; set; }    // required if Resolved
    public List<FileDto> Proofs { get; set; } = new(); // attachments when resolving
}