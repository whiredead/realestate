namespace ProjectAPI.Domain.Sales.Entities;

public enum ClaimStatus { New = 0, Read = 1, InProgress = 2, Resolved = 3, Rejected = 4 }
public enum ClaimPriority { Low = 0, Normal = 1, High = 2, Critical = 3 }
public enum ClaimCategory { General = 0, Electricity = 1, Plumbing = 2, Finishing = 3, DoorsWindows = 4, Other = 99 }

public class AfterSaleClaim
{
    public Guid Id { get; set; }

    // Unit/ownership
    public Guid UnitId { get; set; }
    public Guid? PurchaseId { get; set; }   // optional: tie to Purchase if you want strict “buyer owns this”

    // Who opened (registered or guest)
    public string? BuyerId { get; set; }    // AspNetUsers.Id
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }

    // Details
    public string Title { get; set; }
    public string Description { get; set; }
    public ClaimCategory Category { get; set; } = ClaimCategory.General;
    public ClaimPriority Priority { get; set; } = ClaimPriority.Normal;
    public ClaimStatus Status { get; set; } = ClaimStatus.New;

    // Assignment & lifecycle
    public string? AssignedAgentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionSummary { get; set; } // filled on resolve

    // Navs
    public ICollection<ClaimAttachment> Attachments { get; set; } = new List<ClaimAttachment>();
    public ICollection<ClaimComment> Comments { get; set; } = new List<ClaimComment>();
    public ICollection<ClaimHistory> History { get; set; } = new List<ClaimHistory>();
}