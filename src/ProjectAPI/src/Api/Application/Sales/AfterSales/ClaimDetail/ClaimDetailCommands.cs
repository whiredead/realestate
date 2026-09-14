using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Handovers.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Sales.AfterSales.ClaimDetail;

/// <summary>Kinds of messages on a claim.</summary>
public static class ClaimCommentKinds
{
    /// <summary>Work carried out, written by the technician (or the lead/admin).</summary>
    public const string WorkDone = "WORK_DONE";
    /// <summary>Internal note by staff.</summary>
    public const string Note = "NOTE";
    /// <summary>The buyer's reply.</summary>
    public const string Reply = "REPLY";
}

/// <summary>Photo phase on a claim attachment.</summary>
public static class ClaimAttachmentPhases
{
    public const string Before = "BEFORE";
    public const string After = "AFTER";
    public static readonly string[] All = { Before, After };
}

/// <summary>
/// Who may read or write on one claim (§6.4): the buyer who filed it; a
/// technician only on a claim assigned to them; admins and the technical lead
/// within their project perimeter. Replaces a check that let ANY internal role
/// (agent, notary, any technician) read and upload evidence on any claim.
/// </summary>
public static class ClaimAccessPolicy
{
    public static bool IsSupervisor(ICurrentUser user) =>
        user.IsGlobalAdmin || user.IsInRole(RoleCodes.ProjectAdmin) || user.IsInRole(RoleCodes.TechLead);

    public static bool IsAssignedTechnician(ICurrentUser user, AfterSaleClaim claim) =>
        user.IsInRole(RoleCodes.Technician) && string.Equals(claim.AssignedAgentId, user.UserId, StringComparison.Ordinal);

    public static bool IsOwner(ICurrentUser user, AfterSaleClaim claim) =>
        !string.IsNullOrEmpty(claim.BuyerId) && string.Equals(claim.BuyerId, user.UserId, StringComparison.Ordinal);

    public static async Task EnsureCanAccessAsync(ApplicationDbContext db, ProjectScopeService scope, ICurrentUser user, AfterSaleClaim claim, CancellationToken ct)
    {
        if (IsOwner(user, claim) || IsAssignedTechnician(user, claim)) return;

        if (IsSupervisor(user))
        {
            await scope.EnsureUnitProjectAccessAsync(claim.UnitId, ct);
            return;
        }

        throw BusinessRuleException.BuyerScopeDenied();
    }
}

// ---------------------------------------------------------------------------
// GET /api/after-sales/claims/{id}
// ---------------------------------------------------------------------------
public class GetClaimDetailQuery : IRequest<ClaimDetailDto>
{
    public Guid ClaimId { get; set; }
}

public class ClaimDetailDto : IHasUnitLocation
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string? BuyerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ClaimCategory Category { get; set; }
    public ClaimPriority Priority { get; set; }
    public ClaimStatus Status { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public string? ResolutionSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<ClaimHistoryDto> History { get; set; } = new();
    public List<ClaimCommentDto> Comments { get; set; } = new();
    public List<ClaimAttachmentDto> Attachments { get; set; } = new();

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ImmeubleId { get; set; }
    public string? ImmeubleName { get; set; }
    public string? FloorName { get; set; }
    public string? UnitNumber { get; set; }
    public UnitContextDto? UnitContext { get; set; }
}

public class ClaimHistoryDto
{
    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? ChangedByName { get; set; }
}

public class ClaimCommentDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = ClaimCommentKinds.Note;
    public string Message { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClaimAttachmentDto
{
    public Guid Id { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? Phase { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class GetClaimDetailHandler : IRequestHandler<GetClaimDetailQuery, ClaimDetailDto>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _scope;
    private readonly ICurrentUser _user;

    public GetClaimDetailHandler(ApplicationDbContext db, ProjectScopeService scope, ICurrentUser user)
    {
        _db = db;
        _scope = scope;
        _user = user;
    }

    public async Task<ClaimDetailDto> Handle(GetClaimDetailQuery request, CancellationToken ct)
    {
        var claim = await _db.Set<AfterSaleClaim>()
            .Include(c => c.Attachments)
            .Include(c => c.Comments)
            .Include(c => c.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, ct)
            ?? throw new NotFoundException($"Claim {request.ClaimId} not found.");

        await ClaimAccessPolicy.EnsureCanAccessAsync(_db, _scope, _user, claim, ct);

        var userIds = claim.Comments.Select(c => c.AuthorUserId)
            .Concat(claim.History.Select(h => h.ChangedByUserId))
            .Append(claim.AssignedAgentId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var context = (await UnitLocations.ForUnitsAsync(_db, new[] { claim.UnitId }, ct)).GetValueOrDefault(claim.UnitId);

        return new ClaimDetailDto
        {
            Id = claim.Id,
            UnitId = claim.UnitId,
            BuyerId = claim.BuyerId,
            Title = claim.Title,
            Description = claim.Description,
            Category = claim.Category,
            Priority = claim.Priority,
            Status = claim.Status,
            AssignedAgentId = claim.AssignedAgentId,
            AssignedAgentName = claim.AssignedAgentId is null ? null : names.GetValueOrDefault(claim.AssignedAgentId),
            ResolutionSummary = claim.ResolutionSummary,
            CreatedAt = claim.CreatedAt,
            ResolvedAt = claim.ResolvedAt,
            ClosedAt = claim.ClosedAt,
            History = claim.History.OrderBy(h => h.ChangedAt).Select(h => new ClaimHistoryDto
            {
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                Note = h.Note,
                ChangedAt = h.ChangedAt,
                ChangedByName = names.GetValueOrDefault(h.ChangedByUserId)
            }).ToList(),
            Comments = claim.Comments.OrderBy(c => c.CreatedAt).Select(c => new ClaimCommentDto
            {
                Id = c.Id,
                Kind = c.Kind,
                Message = c.Message,
                AuthorUserId = c.AuthorUserId,
                AuthorName = names.GetValueOrDefault(c.AuthorUserId),
                CreatedAt = c.CreatedAt
            }).ToList(),
            Attachments = claim.Attachments.OrderBy(a => a.UploadedAt).Select(a => new ClaimAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Phase = a.Phase,
                UploadedAt = a.UploadedAt
            }).ToList()
        }.WithLocation(context);
    }
}

// ---------------------------------------------------------------------------
// POST /api/after-sales/claims/{id}/comments
// ---------------------------------------------------------------------------
public class AddClaimCommentCommand : IRequest<ClaimCommentDto>
{
    public Guid ClaimId { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>WORK_DONE or NOTE for staff; ignored for the buyer (always REPLY).</summary>
    public string? Kind { get; set; }
}

public class AddClaimCommentHandler : IRequestHandler<AddClaimCommentCommand, ClaimCommentDto>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _scope;
    private readonly ICurrentUser _user;

    public AddClaimCommentHandler(ApplicationDbContext db, ProjectScopeService scope, ICurrentUser user)
    {
        _db = db;
        _scope = scope;
        _user = user;
    }

    public async Task<ClaimCommentDto> Handle(AddClaimCommentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Message", "Le message est obligatoire.") });
        }
        if (request.Message.Length > 2000)
        {
            throw new Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Message", "Le message ne peut dépasser 2000 caractères.") });
        }

        var claim = await _db.Set<AfterSaleClaim>().FirstOrDefaultAsync(c => c.Id == request.ClaimId, ct)
            ?? throw new NotFoundException($"Claim {request.ClaimId} not found.");

        await ClaimAccessPolicy.EnsureCanAccessAsync(_db, _scope, _user, claim, ct);

        if (claim.Status is ClaimStatus.Closed or ClaimStatus.Cancelled or ClaimStatus.Rejected)
        {
            throw new BusinessRuleException(BusinessErrorCodes.InvalidStatusTransition,
                "La réclamation est clôturée : aucun message ne peut plus y être ajouté.", StatusCodes.Status409Conflict);
        }

        var isStaff = ClaimAccessPolicy.IsSupervisor(_user) || ClaimAccessPolicy.IsAssignedTechnician(_user, claim);
        var kind = isStaff
            ? (string.Equals(request.Kind, ClaimCommentKinds.WorkDone, StringComparison.OrdinalIgnoreCase) ? ClaimCommentKinds.WorkDone : ClaimCommentKinds.Note)
            : ClaimCommentKinds.Reply;

        var comment = new ClaimComment
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            AuthorUserId = _user.UserId!,
            Message = request.Message.Trim(),
            Kind = kind,
            CreatedAt = DateTime.UtcNow
        };
        _db.Add(comment);
        claim.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ClaimCommentDto { Id = comment.Id, Kind = kind, Message = comment.Message, AuthorUserId = comment.AuthorUserId, CreatedAt = comment.CreatedAt };
    }
}

// ---------------------------------------------------------------------------
// GET /api/after-sales/claims/eligible-units
// ---------------------------------------------------------------------------
public class GetClaimEligibleUnitsQuery : IRequest<List<ClaimEligibleUnitDto>> { }

/// <summary>A unit the signed-in buyer may file a claim on, with where it is and its sale.</summary>
public class ClaimEligibleUnitDto
{
    public Guid UnitId { get; set; }
    public Guid SaleId { get; set; }
    public DateTime WarrantyEndsAt { get; set; }
    public UnitContextDto UnitContext { get; set; } = new();
}

public class GetClaimEligibleUnitsHandler : IRequestHandler<GetClaimEligibleUnitsQuery, List<ClaimEligibleUnitDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _user;

    public GetClaimEligibleUnitsHandler(ApplicationDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<List<ClaimEligibleUnitDto>> Handle(GetClaimEligibleUnitsQuery request, CancellationToken ct)
    {
        var userId = _user.UserId;
        if (string.IsNullOrEmpty(userId)) return new();
        var now = DateTime.UtcNow;

        // Confirmed sale of a converted reservation owned by the caller, on a
        // delivered unit, with an active warranty — the same rule as claim creation.
        var rows = await (
            from s in _db.Set<Sale>()
            join r in _db.Set<Reservation>() on s.ReservationId equals r.Id
            join u in _db.Set<UnitEntity>() on s.UnitId equals u.Id
            where s.Status == SaleStatus.Confirmed
               && r.BuyerId == userId
               && u.Status == UnitCommercialStatus.Delivered
            select new { s.UnitId, SaleId = s.Id }
        ).ToListAsync(ct);

        var unitIds = rows.Select(r => r.UnitId).Distinct().ToList();
        var warranties = await _db.Warranties
            .Where(w => unitIds.Contains(w.UnitId) && w.IsActive && w.EndsAt > now)
            .GroupBy(w => w.UnitId)
            .Select(g => new { UnitId = g.Key, EndsAt = g.Max(w => w.EndsAt) })
            .ToDictionaryAsync(x => x.UnitId, x => x.EndsAt, ct);

        var contexts = await UnitLocations.ForUnitsAsync(_db, warranties.Keys, ct);

        return rows
            .Where(r => warranties.ContainsKey(r.UnitId) && contexts.ContainsKey(r.UnitId))
            .GroupBy(r => r.UnitId)
            .Select(g => new ClaimEligibleUnitDto
            {
                UnitId = g.Key,
                SaleId = g.First().SaleId,
                WarrantyEndsAt = warranties[g.Key],
                UnitContext = contexts[g.Key]
            })
            .ToList();
    }
}
