using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Audit.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.AuditLogs;

public sealed record AuditLogDto(
    Guid Id,
    DateTime OccurredAt,
    string Category,
    string Action,
    string Outcome,
    string? ActorUserId,
    string? ActorName,
    string? ActorEmail,
    string? ActorRoles,
    string? EntityType,
    string? EntityId,
    /// <summary>Readable name of the main record when it is a user or a project.</summary>
    string? EntityName,
    Guid? ProjectId,
    string? ProjectName,
    JsonElement? Changes,
    string? Detail,
    /// <summary>Caller IP; shown for security events.</summary>
    string? IpAddress);

/// <summary>
/// GET /api/audit-logs — the journal, newest first (§5.3, §28 "Audit" / "Global audit").
///
/// Perimeter (§6.4): GLOBAL_ADMIN sees everything, security events included.
/// PROJECT_ADMIN sees only business rows attached to a project they are a member of;
/// rows without a project (users, referentials, permissions) stay global-only.
/// </summary>
public class GetAuditLogsQuery : IRequest<PaginatedResponse<AuditLogDto>>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public Guid? ProjectId { get; set; }
    /// <summary>
    /// Actions performed by this user. Deliberately NOT named "ActorUserId": ActorStampingBehaviour
    /// overwrites that name with the caller's id, which silently filtered the journal to the viewer.
    /// </summary>
    public string? ByUserId { get; set; }
    /// <summary>With <see cref="EntityId"/>: the history of one record. "Project" means everything in that project.</summary>
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public string? Category { get; set; }
    public string? Outcome { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class GetAuditLogsHandler : IRequestHandler<GetAuditLogsQuery, PaginatedResponse<AuditLogDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ProjectScopeService _scope;

    public GetAuditLogsHandler(ApplicationDbContext db, ICurrentUser user, ProjectScopeService scope)
    {
        _db = db;
        _user = user;
        _scope = scope;
    }

    public async Task<PaginatedResponse<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var query = await AuditLogScope.VisibleAsync(_db, _user, _scope, ct);

        if (request.ProjectId is { } projectId) query = query.Where(a => a.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(request.ByUserId)) query = query.Where(a => a.ActorUserId == request.ByUserId);
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(a => a.Action == request.Action);
        if (!string.IsNullOrWhiteSpace(request.Category)) query = query.Where(a => a.Category == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Outcome)) query = query.Where(a => a.Outcome == request.Outcome);
        if (request.From is { } from) query = query.Where(a => a.OccurredAt >= from);
        if (request.To is { } to) query = query.Where(a => a.OccurredAt < to);

        if (!string.IsNullOrWhiteSpace(request.EntityType) && !string.IsNullOrWhiteSpace(request.EntityId))
        {
            var type = request.EntityType;
            var id = request.EntityId;
            if (type == "Project" && Guid.TryParse(id, out var pid))
                query = query.Where(a => a.ProjectId == pid || a.Subjects.Any(s => s.EntityType == type && s.EntityId == id));
            else if (type == "User")
                // A user's history: what they did, and what was done to their account.
                query = query.Where(a => a.ActorUserId == id || a.Subjects.Any(s => s.EntityType == type && s.EntityId == id));
            else
                query = query.Where(a => a.Subjects.Any(s => s.EntityType == type && s.EntityId == id));
        }

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(a => new
            {
                Log = a,
                Actor = _db.Users.Where(u => u.Id == a.ActorUserId).Select(u => new { u.FirstName, u.LastName, u.Email }).FirstOrDefault(),
                ProjectName = _db.Projects.Where(p => p.Id == a.ProjectId).Select(p => p.Name).FirstOrDefault(),
                TargetUser = a.EntityType == "User" ? _db.Users.Where(u => u.Id == a.EntityId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() : null,
                TargetProject = a.EntityType == "Project" ? _db.Projects.Where(p => p.Id.ToString() == a.EntityId).Select(p => p.Name).FirstOrDefault() : null,
                // A reservation reads as its buyer and unit, an appointment as its prospect, a claim as its title.
                TargetReservation = a.EntityType == "Reservation"
                    ? _db.Reservations.Where(r => r.Id.ToString() == a.EntityId)
                        .Select(r => (r.Name ?? "") + " " + (r.LastName ?? "") + " · " + _db.Units.Where(u => u.Id == r.UnitId).Select(u => u.UnitNumber).FirstOrDefault())
                        .FirstOrDefault()
                    : null,
                TargetAppointment = a.EntityType == "Appointment" ? _db.Appointments.Where(p => p.Id.ToString() == a.EntityId).Select(p => p.Name + " " + p.LastName).FirstOrDefault() : null,
                TargetClaim = a.EntityType == "AfterSaleClaim" ? _db.AfterSaleClaims.Where(c => c.Id.ToString() == a.EntityId).Select(c => c.Title).FirstOrDefault() : null,
                // A claim message reads as its claim; a visit report as the visit it reports on.
                TargetComment = a.EntityType == "ClaimComment"
                    ? _db.ClaimComments.Where(m => m.Id.ToString() == a.EntityId).Select(m => _db.AfterSaleClaims.Where(c => c.Id == m.ClaimId).Select(c => c.Title).FirstOrDefault()).FirstOrDefault()
                    : null,
                TargetVisitReport = a.EntityType == "AppointmentVisitReport"
                    ? _db.AppointmentVisitReports.Where(v => v.Id.ToString() == a.EntityId).Select(v => _db.Appointments.Where(p => p.Id == v.AppointmentId).Select(p => p.Name + " " + p.LastName).FirstOrDefault()).FirstOrDefault()
                    : null
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var data = rows.Select(r => new AuditLogDto(
            r.Log.Id, r.Log.OccurredAt, r.Log.Category, r.Log.Action, r.Log.Outcome,
            r.Log.ActorUserId,
            r.Actor is null ? r.Log.ActorLabel : $"{r.Actor.FirstName} {r.Actor.LastName}".Trim(),
            r.Actor?.Email ?? r.Log.ActorLabel,
            r.Log.ActorRoles, r.Log.EntityType, r.Log.EntityId, (r.TargetUser ?? r.TargetProject ?? r.TargetReservation ?? r.TargetAppointment ?? r.TargetClaim ?? r.TargetComment ?? r.TargetVisitReport)?.Trim(), r.Log.ProjectId, r.ProjectName,
            ParseChanges(r.Log.Changes), r.Log.Detail, r.Log.IpAddress)).ToList();

        return new PaginatedResponse<AuditLogDto>(data, request.PageNumber, request.PageSize, total);
    }

    private static JsonElement? ParseChanges(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

/// <summary>GET /api/audit-logs/actions — the action codes present in the caller's perimeter, for the filter list.</summary>
public class GetAuditActionsQuery : IRequest<List<string>> { }

public class GetAuditActionsHandler : IRequestHandler<GetAuditActionsQuery, List<string>>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ProjectScopeService _scope;

    public GetAuditActionsHandler(ApplicationDbContext db, ICurrentUser user, ProjectScopeService scope)
    {
        _db = db;
        _user = user;
        _scope = scope;
    }

    public async Task<List<string>> Handle(GetAuditActionsQuery request, CancellationToken ct) =>
        await (await AuditLogScope.VisibleAsync(_db, _user, _scope, ct))
            .Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct);
}

internal static class AuditLogScope
{
    public static async Task<IQueryable<AuditLog>> VisibleAsync(ApplicationDbContext db, ICurrentUser user, ProjectScopeService scope, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking();
        var projects = await scope.GetScopedProjectIdsAsync(ct);
        if (projects is null) return query; // GLOBAL_ADMIN
        return query.Where(a => a.Category == AuditCategories.Business && a.ProjectId != null && projects.Contains(a.ProjectId.Value));
    }
}
