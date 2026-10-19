using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Projects.NotaryAppointments;

/// <summary>
/// Opens or closes notary appointments on a project. Closed, no appointment
/// can be requested or confirmed on its reservations (see
/// NotaryEligibilityService). Appointments already confirmed are untouched.
/// </summary>
public class SetProjectNotaryAppointmentsCommand : IRequest<SetProjectNotaryAppointmentsResponse>
{
    public Guid ProjectId { get; set; }
    public bool Open { get; set; }
}

public class SetProjectNotaryAppointmentsResponse
{
    public Guid ProjectId { get; set; }
    public bool NotaryAppointmentsOpen { get; set; }
}

public class SetProjectNotaryAppointmentsHandler
    : IRequestHandler<SetProjectNotaryAppointmentsCommand, SetProjectNotaryAppointmentsResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public SetProjectNotaryAppointmentsHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<SetProjectNotaryAppointmentsResponse> Handle(
        SetProjectNotaryAppointmentsCommand request, CancellationToken ct)
    {
        var project = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        await _projectScope.EnsureProjectAccessAsync(project.Id, ct);

        project.NotaryAppointmentsOpen = request.Open;
        await _db.SaveChangesAsync(ct);

        return new SetProjectNotaryAppointmentsResponse
        {
            ProjectId = project.Id,
            NotaryAppointmentsOpen = project.NotaryAppointmentsOpen
        };
    }
}
