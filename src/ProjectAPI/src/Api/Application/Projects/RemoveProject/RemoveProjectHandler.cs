using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Projects.RemoveProject;

/// <summary>
/// Deletes a project that carries no business history, together with its whole
/// configuration, in one transaction.
///
/// Refused (409) as soon as the project has anything a business record depends
/// on: a reservation, a sale or a delivery on one of its units, a commercial
/// appointment, or buyer feedback on one of its buildings — its end of life is
/// then finalisation, which keeps the record. A finalised project is refused too
/// (read-only).
///
/// It used to load every building, unit, reservation, sale and appointment of
/// the whole database into memory, delete only buildings/units/appointments, and
/// then fail on the first configuration row still pointing at the project
/// (memberships, milestones, document rules, videos, type links…): every real
/// project — they all have those — was undeletable, with a 409 RESOURCE_IN_USE.
/// </summary>
public class RemoveProjectHandler : IRequestHandler<RemoveProjectCommand, RemoveProjectResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public RemoveProjectHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    /// <summary>Configuration rows removed with the project, children first. {0} is the project id.</summary>
    private static readonly (string Label, string Sql)[] Cascade =
    {
        ("UnitStatusHistories", "DELETE h FROM UnitStatusHistories h JOIN Units u ON u.Id = h.UnitId JOIN Immeubles i ON i.Id = u.ProjectId WHERE i.ProjectId = {0}"),
        ("UnitTitleHistories", "DELETE h FROM UnitTitleHistories h JOIN Units u ON u.Id = h.UnitId JOIN Immeubles i ON i.Id = u.ProjectId WHERE i.ProjectId = {0}"),
        ("UnitTitleStates", "DELETE t FROM UnitTitleStates t JOIN Units u ON u.Id = t.UnitId JOIN Immeubles i ON i.Id = u.ProjectId WHERE i.ProjectId = {0}"),
        ("UnitTracking", "DELETE t FROM UnitTracking t JOIN Units u ON u.Id = t.UnitId JOIN Immeubles i ON i.Id = u.ProjectId WHERE i.ProjectId = {0}"),
        ("Units", "DELETE u FROM Units u JOIN Immeubles i ON i.Id = u.ProjectId WHERE i.ProjectId = {0}"),
        ("Floors", "DELETE f FROM Floors f JOIN Immeubles i ON i.Id = f.ImmeubleId WHERE i.ProjectId = {0}"),
        ("ImmeubleFeature", "DELETE x FROM ImmeubleFeature x JOIN Immeubles i ON i.Id = x.ImmeubleId WHERE i.ProjectId = {0}"),
        ("ImmeublePlanInterieurs", "DELETE x FROM ImmeublePlanInterieurs x JOIN Immeubles i ON i.Id = x.ImmeubleId WHERE i.ProjectId = {0}"),
        ("ImmeubleTracking", "DELETE x FROM ImmeubleTracking x JOIN Immeubles i ON i.Id = x.ImmeubleId WHERE i.ProjectId = {0}"),
        ("ImmeubleTypeBien", "DELETE x FROM ImmeubleTypeBien x JOIN Immeubles i ON i.Id = x.ImmeubleId WHERE i.ProjectId = {0}"),
        // Assignments.ProjectId is the FK to the building (legacy naming).
        ("Assignments", "DELETE x FROM Assignments x JOIN Immeubles i ON i.Id = x.ProjectId WHERE i.ProjectId = {0}"),
        ("Immeubles", "DELETE FROM Immeubles WHERE ProjectId = {0}"),
        ("ConstructionUpdates", "DELETE FROM ConstructionUpdates WHERE ProjectId = {0}"),
        ("ConstructionMilestones", "DELETE FROM ConstructionMilestones WHERE ProjectId = {0}"),
        ("EspaceTempsReel", "DELETE FROM EspaceTempsReel WHERE ProjectId = {0}"),
        ("InternalInvitationProjectAssignments", "DELETE FROM InternalInvitationProjectAssignments WHERE ProjectId = {0}"),
        ("Leads", "DELETE FROM Leads WHERE ProjectId = {0}"),
        ("LikedProjects", "DELETE FROM LikedProjects WHERE ProjectId = {0}"),
        ("ProjectAgentAssignmentConfigs", "DELETE FROM ProjectAgentAssignmentConfigs WHERE ProjectId = {0}"),
        ("ProjectAssignments", "DELETE FROM ProjectAssignments WHERE ProjectId = {0}"),
        ("ProjectFeature", "DELETE FROM ProjectFeature WHERE ProjectId = {0}"),
        ("ProjectTypeBiens", "DELETE FROM ProjectTypeBiens WHERE ProjectId = {0}"),
        ("QuartierAmenities", "DELETE FROM QuartierAmenities WHERE ProjectId = {0}"),
        ("ProjectDocumentRequirements", "DELETE FROM ProjectDocumentRequirements WHERE ProjectId = {0}"),
        ("ProjectMemberships", "DELETE FROM ProjectMemberships WHERE ProjectId = {0}"),
        ("Projects", "DELETE FROM Projects WHERE Id = {0}"),
    };

    public async Task<RemoveProjectResponse> Handle(RemoveProjectCommand request, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == request.ProjectId)
            .Select(p => new { p.Id, p.Name, p.StatusGlobal })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        // §6.4 — the highest-blast-radius mutation: never outside the caller's perimeter.
        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, ct);

        if (ProjectStatusCodes.IsReadOnly(project.StatusGlobal))
        {
            throw new ProjectReadOnlyException(project.Id);
        }

        var id = request.ProjectId;
        var unitIds = _db.Set<Domain.Immeubles.Entities.Unit>()
            .Join(_db.Set<Domain.Immeubles.Entities.Immeuble>(), u => u.ProjectId, i => i.Id, (u, i) => new { u.Id, i.ProjectId })
            .Where(x => x.ProjectId == id)
            .Select(x => x.Id);

        var hasHistory =
            await _db.Set<Domain.Reservations.Entities.Reservation>().AnyAsync(r => unitIds.Contains(r.UnitId), ct)
            || await _db.Set<Domain.Sales.Entities.Sale>().AnyAsync(s => unitIds.Contains(s.UnitId), ct)
            || await _db.Set<Domain.Appointments.Entities.Appointment>().AnyAsync(a => a.ProjectId == id, ct)
            || await _db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM PropertyDeliveries d JOIN Units u ON u.Id = d.UnitId JOIN Immeubles i ON i.Id = u.ProjectId WHERE i.ProjectId = {id}").SingleAsync(ct) > 0
            || await _db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM Feedbacks f JOIN Immeubles i ON i.Id = f.ProjectId WHERE i.ProjectId = {id}").SingleAsync(ct) > 0
            || await _db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM Appointments a JOIN Immeubles i ON i.Id = a.ImmeubleId WHERE i.ProjectId = {id}").SingleAsync(ct) > 0;

        if (hasHistory)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ResourceInUse,
                "Ce projet porte déjà des réservations, ventes, rendez-vous ou avis : il ne peut pas être supprimé, seulement finalisé.",
                StatusCodes.Status409Conflict);
        }

        var deleted = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        foreach (var (label, sql) in Cascade)
        {
            var rows = await _db.Database.ExecuteSqlRawAsync(sql.Replace("{0}", "@projectId"), new[] { new Microsoft.Data.SqlClient.SqlParameter("@projectId", id) }, ct);
            if (rows > 0) deleted.Add($"{label}: {rows}");
        }
        await tx.CommitAsync(ct);

        return new RemoveProjectResponse
        {
            Success = true,
            Message = $"Project '{project.Name}' deleted successfully with all related entities.",
            DeletedEntities = deleted,
            Details = new List<string>
            {
                $"Total rows deleted: {deleted.Sum(d => int.Parse(d[(d.LastIndexOf(' ') + 1)..]))}",
                $"Deleted at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
            }
        };
    }
}
