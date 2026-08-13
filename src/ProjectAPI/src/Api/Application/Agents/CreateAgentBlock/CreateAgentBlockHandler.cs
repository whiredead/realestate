using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.CreateAgentBlock;

public class CreateAgentBlockHandler :
    IRequestHandler<CreateAgentBlockCommand, CreateAgentBlockResponse>
{
    private readonly IAgentBlockRepository _blockRepo;
    private readonly IAppointmentRepository _apptRepo;
    private readonly ProjectScopeService _projectScope;

    public CreateAgentBlockHandler(
        IAgentBlockRepository blockRepo,
        IAppointmentRepository apptRepo,
        ProjectScopeService projectScope)
    {
        _blockRepo = blockRepo;
        _apptRepo = apptRepo;
        _projectScope = projectScope;
    }

    public async Task<CreateAgentBlockResponse> Handle(CreateAgentBlockCommand req, CancellationToken ct)
    {
        // §6.3 — AgentId is caller-supplied in the request body; a SALES_AGENT
        // caller must be blocking their own calendar, never another's.
        _projectScope.EnsureAgentOwnsCalendar(req.AgentId);

        if (req.End <= req.Start)
            throw new ArgumentException("End must be after Start.");

        var startUtc = DateTime.SpecifyKind(req.Start, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(req.End, DateTimeKind.Utc);

        // Reject if any blocking-status appointment for this agent falls inside the block.
        var conflicting = await _apptRepo.Find(a =>
            a.SalesAgentId == req.AgentId &&
            a.AppointmentDate >= startUtc &&
            a.AppointmentDate < endUtc &&
            a.Status != nameof(AppointmentAttemptStatus.Cancelled) &&
            a.Status != nameof(AppointmentAttemptStatus.Rejected) &&
            a.Status != nameof(AppointmentAttemptStatus.Completed) &&
            a.Status != nameof(AppointmentAttemptStatus.NoShow) &&
            a.Status != nameof(AppointmentAttemptStatus.Superseded)
        );

        if (conflicting.Any())
        {
            var times = string.Join(", ", conflicting.Select(a => a.AppointmentDate.ToString("u")));
            throw new InvalidOperationException($"Cannot block over existing appointments: {times}");
        }

        var block = new AgentBlock
        {
            Id = Guid.NewGuid(),
            AgentId = req.AgentId,
            Start = startUtc,
            End = endUtc,
            Reason = req.Reason
        };

        await _blockRepo.InsertAsync(block);
        await _blockRepo.SaveAsync();

        return new CreateAgentBlockResponse
        {
            Id = block.Id,
            StartUtc = block.Start,
            EndUtc = block.End,
            Reason = block.Reason
        };
    }
}
