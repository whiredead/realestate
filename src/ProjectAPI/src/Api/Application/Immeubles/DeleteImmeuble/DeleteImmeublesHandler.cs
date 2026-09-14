using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Immeubles.DeleteImmeuble;

/// <summary>
/// Deletes a building that carries no business history, with its floors, units
/// and configuration, in one transaction.
///
/// It used to delete the building's SALES and RESERVATIONS first — erasing
/// commercial records — one repository save at a time (no transaction), after
/// loading every unit, sale and reservation of the database into memory; it then
/// failed on the first configuration row still referencing a unit or the
/// building. Now a reservation, sale, delivery, commercial appointment or buyer
/// feedback on the building refuses the deletion (409); a building of a
/// finalised project is read-only.
/// </summary>
public class DeleteImmeublesHandler : IRequestHandler<DeleteImmeublesCommand, DeleteImmeubleResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public DeleteImmeublesHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    /// <summary>Rows removed with the building, children first. @id is the building id.</summary>
    private static readonly (string Label, string Sql)[] Cascade =
    {
        ("UnitStatusHistories", "DELETE h FROM UnitStatusHistories h JOIN Units u ON u.Id = h.UnitId WHERE u.ProjectId = @id"),
        ("UnitTitleHistories", "DELETE h FROM UnitTitleHistories h JOIN Units u ON u.Id = h.UnitId WHERE u.ProjectId = @id"),
        ("UnitTitleStates", "DELETE t FROM UnitTitleStates t JOIN Units u ON u.Id = t.UnitId WHERE u.ProjectId = @id"),
        ("UnitTracking", "DELETE t FROM UnitTracking t JOIN Units u ON u.Id = t.UnitId WHERE u.ProjectId = @id"),
        // Unit.ProjectId is the FK to the building.
        ("Units", "DELETE FROM Units WHERE ProjectId = @id"),
        ("Floors", "DELETE FROM Floors WHERE ImmeubleId = @id"),
        ("ImmeubleFeature", "DELETE FROM ImmeubleFeature WHERE ImmeubleId = @id"),
        ("ImmeublePlanInterieurs", "DELETE FROM ImmeublePlanInterieurs WHERE ImmeubleId = @id"),
        ("ImmeubleTracking", "DELETE FROM ImmeubleTracking WHERE ImmeubleId = @id"),
        ("ImmeubleTypeBien", "DELETE FROM ImmeubleTypeBien WHERE ImmeubleId = @id"),
        ("Assignments", "DELETE FROM Assignments WHERE ProjectId = @id"),
        ("Immeubles", "DELETE FROM Immeubles WHERE Id = @id"),
    };

    public async Task<DeleteImmeubleResponse> Handle(DeleteImmeublesCommand request, CancellationToken ct)
    {
        var immeuble = await _db.Set<Domain.Immeubles.Entities.Immeuble>().AsNoTracking()
            .Where(i => i.Id == request.Id)
            .Select(i => new { i.Id, i.Name, i.ProjectId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Immeuble {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(immeuble.ProjectId, ct);

        var projectStatus = await _db.Projects.AsNoTracking().Where(p => p.Id == immeuble.ProjectId).Select(p => p.StatusGlobal).FirstOrDefaultAsync(ct);
        if (ProjectStatusCodes.IsReadOnly(projectStatus))
        {
            throw new ProjectReadOnlyException(immeuble.ProjectId);
        }

        var id = request.Id;
        var unitIds = _db.Set<Domain.Immeubles.Entities.Unit>().Where(u => u.ProjectId == id).Select(u => u.Id);
        var hasHistory =
            await _db.Set<Domain.Reservations.Entities.Reservation>().AnyAsync(r => unitIds.Contains(r.UnitId), ct)
            || await _db.Set<Domain.Sales.Entities.Sale>().AnyAsync(s => unitIds.Contains(s.UnitId), ct)
            || await _db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM PropertyDeliveries d JOIN Units u ON u.Id = d.UnitId WHERE u.ProjectId = {id}").SingleAsync(ct) > 0
            || await _db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM Feedbacks WHERE ProjectId = {id}").SingleAsync(ct) > 0
            || await _db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM Appointments WHERE ImmeubleId = {id}").SingleAsync(ct) > 0;

        if (hasHistory)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ResourceInUse,
                "Ce bâtiment porte déjà des réservations, ventes, rendez-vous ou avis : il ne peut pas être supprimé.",
                StatusCodes.Status409Conflict);
        }

        var details = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        foreach (var (label, sql) in Cascade)
        {
            var rows = await _db.Database.ExecuteSqlRawAsync(sql, new[] { new SqlParameter("@id", id) }, ct);
            if (rows > 0) details.Add($"{label}: {rows}");
        }
        await tx.CommitAsync(ct);

        details.Add($"Deleted at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        return new DeleteImmeubleResponse
        {
            Success = true,
            Message = $"Immeuble named '{immeuble.Name}' (ID: {request.Id}) deleted successfully with all related entities.",
            Details = details
        };
    }
}
