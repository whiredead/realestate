using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.ValidateMilestone;

public class ValidateMilestoneCommand : IRequest<ValidateMilestoneResponse> { public Guid MilestoneId { get; set; } }
public class ValidateMilestoneResponse { public Guid MilestoneId { get; set; } public bool IsValidated { get; set; } }
public class ValidateMilestoneHandler : IRequestHandler<ValidateMilestoneCommand, ValidateMilestoneResponse>
{
    private readonly ApplicationDbContext _db; private readonly ProjectScopeService _scope;
    public ValidateMilestoneHandler(ApplicationDbContext db, ProjectScopeService scope) { _db = db; _scope = scope; }
    public async Task<ValidateMilestoneResponse> Handle(ValidateMilestoneCommand request, CancellationToken ct)
    {
        var milestone = await _db.Set<ConstructionMilestone>().FirstOrDefaultAsync(x => x.Id == request.MilestoneId, ct) ?? throw new KeyNotFoundException("Jalon introuvable.");
        await _scope.EnsureProjectAccessAsync(milestone.ProjectId, ct);
        if (milestone.Status != MilestoneStatus.Completed) throw new InvalidOperationException("Le jalon doit être terminé avant validation.");
        milestone.IsValidated = true; await _db.SaveChangesAsync(ct);
        return new ValidateMilestoneResponse { MilestoneId = milestone.Id, IsValidated = true };
    }
}
