using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.SetAgentAppointmentSettings;

public class SetAgentAppointmentSettingsHandler
    : IRequestHandler<SetAgentAppointmentSettingsCommand, SetAgentAppointmentSettingsResponse>
{
    private readonly IAgentAppointmentSettingsRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public SetAgentAppointmentSettingsHandler(IAgentAppointmentSettingsRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<SetAgentAppointmentSettingsResponse> Handle(SetAgentAppointmentSettingsCommand req, CancellationToken ct)
    {
        // §6.3 — AgentId comes from the route; a SALES_AGENT caller must only
        // set their own appointment duration/buffer, never another agent's.
        _projectScope.EnsureAgentOwnsCalendar(req.AgentId);

        if (req.DurationMinutes <= 0)
            throw new ArgumentException("DurationMinutes must be positive.");
        if (req.BufferMinutes < 0)
            throw new ArgumentException("BufferMinutes cannot be negative.");

        var existing = (await _repo.Find(s => s.AgentId == req.AgentId)).FirstOrDefault();

        if (existing != null)
        {
            existing.DurationMinutes = req.DurationMinutes;
            existing.BufferMinutes = req.BufferMinutes;
            await _repo.Update(existing);
        }
        else
        {
            existing = new AgentAppointmentSettings
            {
                Id = Guid.NewGuid(),
                AgentId = req.AgentId,
                DurationMinutes = req.DurationMinutes,
                BufferMinutes = req.BufferMinutes
            };
            await _repo.InsertAsync(existing);
        }

        await _repo.SaveAsync();

        return new SetAgentAppointmentSettingsResponse
        {
            AgentId = req.AgentId,
            DurationMinutes = existing.DurationMinutes,
            BufferMinutes = existing.BufferMinutes
        };
    }
}
