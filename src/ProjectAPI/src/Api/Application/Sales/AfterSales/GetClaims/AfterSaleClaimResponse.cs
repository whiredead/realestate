using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;

public class AfterSaleClaimResponse : Common.Units.IHasUnitLocation
{
    // Location of the unit (projet → immeuble → étage → unité).
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ImmeubleId { get; set; }
    public string? ImmeubleName { get; set; }
    public string? FloorName { get; set; }
    public string? UnitNumber { get; set; }
    public ProjectAPI.Api.Application.Common.Units.UnitContextDto? UnitContext { get; set; }

    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string Title { get; set; }
    public ClaimCategory Category { get; set; }
    public ClaimPriority Priority { get; set; }
    public ClaimStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Technician the claim is assigned to (drives which actions the SAV screen offers).</summary>
    public string? AssignedAgentId { get; set; }
    public IEnumerable<string> AttachmentUrls { get; set; } = Enumerable.Empty<string>();
}