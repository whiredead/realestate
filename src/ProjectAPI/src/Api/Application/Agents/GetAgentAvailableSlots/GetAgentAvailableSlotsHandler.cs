using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Agents.GetAgentAvailableSlots;

/// <summary>
/// Composes an agent's bookable slots from: the recurring weekly template,
/// minus ad-hoc blocks, minus/plus whole-day date overrides (leave, closure,
/// or extra availability), minus existing blocking-status appointments — then
/// slices what's left into discrete slots of the agent's configured duration
/// (falling back to <see cref="AgentAppointmentSettings.DefaultDurationMinutes"/>),
/// each followed by the agent's buffer before the next slot can start.
///
/// Structurally mirrors GetNotaryCalendarHandler's window-subtraction
/// approach, but goes further: notary's version treats every appointment as
/// a flat 1-minute busy window and has no duration/buffer or date-override
/// concept at all (NotaryDateDisponibilite is declared but never wired into
/// that handler, or into the DbContext model — an existing gap this handler
/// does not repeat).
/// </summary>
public class GetAgentAvailableSlotsHandler
    : IRequestHandler<GetAgentAvailableSlotsQuery, List<AgentDayAvailabilityDto>>
{
    private readonly IAgentWeeklyAvailabilityRepository _weeklyRepo;
    private readonly IAgentBlockRepository _blockRepo;
    private readonly IAgentDateOverrideRepository _overrideRepo;
    private readonly IAgentAppointmentSettingsRepository _settingsRepo;
    private readonly IAppointmentRepository _apptRepo;

    public GetAgentAvailableSlotsHandler(
        IAgentWeeklyAvailabilityRepository weeklyRepo,
        IAgentBlockRepository blockRepo,
        IAgentDateOverrideRepository overrideRepo,
        IAgentAppointmentSettingsRepository settingsRepo,
        IAppointmentRepository apptRepo)
    {
        _weeklyRepo = weeklyRepo;
        _blockRepo = blockRepo;
        _overrideRepo = overrideRepo;
        _settingsRepo = settingsRepo;
        _apptRepo = apptRepo;
    }

    public async Task<List<AgentDayAvailabilityDto>> Handle(GetAgentAvailableSlotsQuery q, CancellationToken ct)
    {
        var weekly = (await _weeklyRepo.Find(w => w.AgentId == q.AgentId)).ToList();

        var blocks = (await _blockRepo.Find(b =>
                b.AgentId == q.AgentId &&
                b.End > q.From &&
                b.Start < q.To))
            .ToList();

        var overrides = (await _overrideRepo.Find(o =>
                o.AgentId == q.AgentId &&
                o.EndDate >= q.From &&
                o.StartDate < q.To))
            .ToList();

        var blockingStatuses = new[]
        {
            nameof(AppointmentAttemptStatus.Requested),
            nameof(AppointmentAttemptStatus.Confirmed),
            nameof(AppointmentAttemptStatus.RescheduleProposed)
        };
        var appts = (await _apptRepo.Find(a =>
                a.SalesAgentId == q.AgentId &&
                a.AppointmentDate >= q.From &&
                a.AppointmentDate < q.To &&
                blockingStatuses.Contains(a.Status)))
            .Select(a => a.AppointmentDate)
            .ToList();

        var settings = (await _settingsRepo.Find(s => s.AgentId == q.AgentId)).FirstOrDefault();
        var durationMinutes = settings?.DurationMinutes ?? AgentAppointmentSettings.DefaultDurationMinutes;
        var bufferMinutes = settings?.BufferMinutes ?? AgentAppointmentSettings.DefaultBufferMinutes;
        var slotStep = TimeSpan.FromMinutes(durationMinutes + bufferMinutes);
        var slotDuration = TimeSpan.FromMinutes(durationMinutes);

        var startDay = DateOnly.FromDateTime(q.From.Date);
        var endDay = DateOnly.FromDateTime(q.To.Date);

        var days = new List<AgentDayAvailabilityDto>();

        for (var day = startDay; day <= endDay; day = day.AddDays(1))
        {
            // A whole-day override replaces the weekly template outright:
            // "Unavailable" clears the day, "Available" opens the full day
            // (00:00-23:59) regardless of what the weekly template says.
            var dayOverride = overrides.FirstOrDefault(o =>
                DateOnly.FromDateTime(o.StartDate) <= day && DateOnly.FromDateTime(o.EndDate) >= day);

            List<(TimeSpan Start, TimeSpan End)> templates;
            if (dayOverride != null)
            {
                templates = dayOverride.Status == "Unavailable"
                    ? new List<(TimeSpan, TimeSpan)>()
                    : new List<(TimeSpan, TimeSpan)> { (TimeSpan.Zero, TimeSpan.FromHours(24)) };
            }
            else
            {
                templates = weekly
                    .Where(w => w.DayOfWeek == day.DayOfWeek())
                    .Select(w => (w.StartTime, w.EndTime))
                    .ToList();
            }

            var freeSlots = new List<TimeSlotDto>();

            foreach (var (startTs, endTs) in templates)
            {
                var windowStart = day.ToDateTime(startTs);
                var windowEnd = day.ToDateTime(endTs);

                var dayBlocks = blocks
                    .Select(b => (bs: Max(windowStart, b.Start), be: Min(windowEnd, b.End)))
                    .Where(w => w.bs < w.be)
                    .ToList();

                var dayAppts = appts
                    .Select(dt => (bs: Max(windowStart, dt), be: Min(windowEnd, dt + slotStep)))
                    .Where(w => w.bs < w.be)
                    .ToList();

                var free = SubtractWindows((windowStart, windowEnd), dayBlocks.Concat(dayAppts));

                // Slice each free window into discrete slotDuration-long slots,
                // stepping by duration+buffer so consecutive bookings never
                // sit closer together than the configured buffer.
                foreach (var (fs, fe) in free)
                {
                    var cursor = fs;
                    while (cursor + slotDuration <= fe)
                    {
                        freeSlots.Add(new TimeSlotDto(cursor.TimeOfDay, (cursor + slotDuration).TimeOfDay));
                        cursor += slotStep;
                    }
                }
            }

            days.Add(new AgentDayAvailabilityDto { Date = day, FreeSlots = freeSlots });
        }

        return days;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static List<(DateTime start, DateTime end)> SubtractWindows(
        (DateTime start, DateTime end) whole,
        IEnumerable<(DateTime bs, DateTime be)> busy)
    {
        var free = new List<(DateTime, DateTime)> { whole };
        foreach (var b in busy.OrderBy(x => x.bs))
        {
            for (int i = 0; i < free.Count; i++)
            {
                var (fs, fe) = free[i];
                if (b.be <= fs || b.bs >= fe) continue;

                free.RemoveAt(i);
                if (b.bs > fs) free.Insert(i++, (fs, b.bs));
                if (b.be < fe) free.Insert(i, (b.be, fe));
            }
        }
        return free;
    }
}

internal static class DateOnlyExtensions
{
    public static DayOfWeek DayOfWeek(this DateOnly d) => d.ToDateTime(TimeOnly.MinValue).DayOfWeek;
    public static DateTime ToDateTime(this DateOnly d, TimeSpan time) => new DateTime(d.Year, d.Month, d.Day).Add(time);
}
