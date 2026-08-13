using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.AddMilestone;

/// <summary>Creates a weighted construction milestone (spec §15.1 FR-CON-001).</summary>
public class AddMilestoneCommand : IRequest<AddMilestoneResponse>
{
    public Guid ProjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameFr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? DescriptionFr { get; set; }
    public string? DescriptionEn { get; set; }
    public int SequenceNo { get; set; }
    public decimal WeightPercent { get; set; }
    public DateTime? PlannedDate { get; set; }
    public bool VisibleToBuyer { get; set; } = true;
    public bool VisibleToPublic { get; set; }
}

public class AddMilestoneResponse
{
    public Guid MilestoneId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AddMilestoneHandler : IRequestHandler<AddMilestoneCommand, AddMilestoneResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public AddMilestoneHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<AddMilestoneResponse> Handle(AddMilestoneCommand request, CancellationToken ct)
    {
        var projectExists = await _db.Set<Project>().AnyAsync(p => p.Id == request.ProjectId, ct);
        if (!projectExists)
        {
            throw new NotFoundException($"Project {request.ProjectId} not found.");
        }

        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, ct);

        if (request.WeightPercent is < 0 or > 100)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Le poids d'un jalon doit être compris entre 0 et 100.");
        }

        var codeTaken = await _db.Set<ConstructionMilestone>()
            .AnyAsync(m => m.ProjectId == request.ProjectId && m.Code == request.Code, ct);

        if (codeTaken)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"Le code de jalon '{request.Code}' existe déjà pour ce projet.");
        }

        var milestone = new ConstructionMilestone
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Code = request.Code,
            NameFr = request.NameFr,
            NameEn = request.NameEn ?? request.NameFr,
            DescriptionFr = request.DescriptionFr,
            DescriptionEn = request.DescriptionEn,
            SequenceNo = request.SequenceNo,
            WeightPercent = request.WeightPercent,
            PlannedDate = request.PlannedDate,
            Status = MilestoneStatus.NotStarted,
            VisibleToBuyer = request.VisibleToBuyer,
            VisibleToPublic = request.VisibleToPublic,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(milestone);
        await _db.SaveChangesAsync(ct);

        return new AddMilestoneResponse { MilestoneId = milestone.Id, Message = "Jalon créé." };
    }
}
