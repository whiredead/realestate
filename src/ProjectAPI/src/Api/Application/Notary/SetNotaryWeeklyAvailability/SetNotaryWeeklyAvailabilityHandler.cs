using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Notary.SetNotaryWeeklyAvailability;

public class SetNotaryWeeklyAvailabilityHandler
    : IRequestHandler<SetNotaryWeeklyAvailabilityCommand, List<NotaryWeeklyAvailabilitySlot>>
{
    private readonly IWeeklyAvailabilityRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public SetNotaryWeeklyAvailabilityHandler(IWeeklyAvailabilityRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<List<NotaryWeeklyAvailabilitySlot>> Handle(SetNotaryWeeklyAvailabilityCommand req, CancellationToken ct)
    {
        // §6.3 — NotaryId comes from the route; a NOTARY caller must only
        // replace their own weekly schedule, never another notary's.
        if (string.IsNullOrEmpty(req.NotaryId))
        {
            throw new Common.Exceptions.ValidationException(new[] { new ValidationFailure(nameof(req.NotaryId), "NotaryId is required.") });
        }
        _projectScope.EnsureNotaryOwnsCalendar(req.NotaryId);

        foreach (var slot in req.Slots)
        {
            if (slot.EndTime <= slot.StartTime)
                throw new ArgumentException($"EndTime must be after StartTime for {slot.DayOfWeek}.");
        }

        // Replace-all, same convention as SetAgentWeeklyAvailabilityHandler.
        var existing = await _repo.Find(w => w.NotaryId == req.NotaryId);
        foreach (var row in existing)
        {
            _repo.Delete(row);
        }

        foreach (var slot in req.Slots)
        {
            await _repo.InsertAsync(new WeeklyAvailability
            {
                Id = Guid.NewGuid(),
                NotaryId = req.NotaryId!,
                DayOfWeek = slot.DayOfWeek,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            });
        }

        await _repo.SaveAsync();

        return req.Slots;
    }
}
