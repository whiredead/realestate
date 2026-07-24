using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.UpdateMilestoneStatus;

/// <summary>Updates a milestone's status and actual date (spec §15.1 FR-CON-002).</summary>
public class UpdateMilestoneStatusCommand : IRequest<UpdateMilestoneStatusResponse>
{
    public Guid MilestoneId { get; set; }
    public MilestoneStatus Status { get; set; }
    public DateTime? ActualDate { get; set; }
}

public class UpdateMilestoneStatusResponse
{
    public Guid MilestoneId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class UpdateMilestoneStatusHandler
    : IRequestHandler<UpdateMilestoneStatusCommand, UpdateMilestoneStatusResponse>
{
    private readonly ApplicationDbContext _db;

    public UpdateMilestoneStatusHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateMilestoneStatusResponse> Handle(
        UpdateMilestoneStatusCommand request,
        CancellationToken ct)
    {
        var milestone = await _db.Set<ConstructionMilestone>()
            .FirstOrDefaultAsync(m => m.Id == request.MilestoneId, ct)
            ?? throw new NotFoundException($"Milestone {request.MilestoneId} not found.");

        milestone.Status = request.Status;

        if (request.Status == MilestoneStatus.Completed)
        {
            milestone.ActualDate = request.ActualDate ?? DateTime.UtcNow;
        }
        else if (request.ActualDate is not null)
        {
            milestone.ActualDate = request.ActualDate;
        }

        await _db.SaveChangesAsync(ct);

        return new UpdateMilestoneStatusResponse
        {
            MilestoneId = milestone.Id,
            Status = milestone.Status.ToString()
        };
    }
}
