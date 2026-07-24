using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Api.Application.Notary.GetNotaryCalendar
{
    public class GetNotaryCalendarHandler
        : IRequestHandler<GetNotaryCalendarQuery, List<NotaryDayAvailabilityDto>>
    {
        private readonly IWeeklyAvailabilityRepository _weeklyRepo;
        private readonly INotaryBlockRepository _blockRepo;
        private readonly INotaryAppointmentRepository _apptRepo;

        public GetNotaryCalendarHandler(
            IWeeklyAvailabilityRepository weeklyRepo,
            INotaryBlockRepository blockRepo,
            INotaryAppointmentRepository apptRepo)
        {
            _weeklyRepo = weeklyRepo;
            _blockRepo = blockRepo;
            _apptRepo = apptRepo;
        }

        public async Task<List<NotaryDayAvailabilityDto>> Handle(
            GetNotaryCalendarQuery q, CancellationToken ct)
        {
            // 1) Load recurring availabilities
            var weekly = (await _weeklyRepo.Find(w => w.NotaryId == q.NotaryId)).ToList();

            // 2) Load one-off blocks that intersect [From, To)
            var blocks = (await _blockRepo.Find(b =>
                    b.NotaryId == q.NotaryId &&
                    b.End > q.From &&
                    b.Start < q.To))
                .ToList();

            // 3) Load existing bookings in [From, To)
            // NOTE: property name assumed NotaryId (not "NotaireId")
            var appts = (await _apptRepo.Find(a =>
                    a.NotaireId == q.NotaryId &&
                    a.AppointmentDate >= q.From &&
                    a.AppointmentDate < q.To))
                .Select(a => a.AppointmentDate)
                .ToList();

            var startDay = DateOnly.FromDateTime(q.From.Date);
            var endDay = DateOnly.FromDateTime(q.To.Date);

            var days = new List<NotaryDayAvailabilityDto>();

            for (var day = startDay; day <= endDay; day = day.AddDays(1))
            {
                // templates for this weekday
                var templates = weekly
                    .Where(w => w.DayOfWeek == day.DayOfWeek)   // DayOfWeek on both sides
                    .Select(w => (w.StartTime, w.EndTime))       // TimeSpan, TimeSpan
                    .ToList();

                var freeSlots = new List<TimeSlotDto>();

                foreach (var tpl in templates)
                {
                    TimeSpan startTs = tpl.StartTime;
                    TimeSpan endTs = tpl.EndTime;

                    DateTime slotStart = day.ToDateTime(startTs);
                    DateTime slotEnd = day.ToDateTime(endTs);

                    // Build busy windows for this day (blocks)
                    var dayBlocks = blocks
                        .Where(b => DateOnly.FromDateTime(b.Start) <= day &&
                                    DateOnly.FromDateTime(b.End) >= day)
                        .Select(b =>
                        {
                            var bs = Max(slotStart, AlignToDay(day, b.Start));
                            var be = Min(slotEnd, AlignToDay(day, b.End));
                            return (bs, be);
                        })
                        .Where(w => w.bs < w.be)
                        .ToList();

                    // Start with one whole window, subtract dayBlocks
                    var avail = SubtractWindows((slotStart, slotEnd), dayBlocks);

                    // Subtract appointments (treat as a 1-minute busy window)
                    var dayApptWindows = appts
                        .Where(dt => DateOnly.FromDateTime(dt) == day)
                        .Select(dt => (dt, dt.AddMinutes(1)))
                        .ToList();

                    avail = SubtractWindows(avail, dayApptWindows);

                    // Convert to TimeSlotDto (TimeSpan)
                    foreach (var (fs, fe) in avail)
                        freeSlots.Add(new TimeSlotDto(fs.TimeOfDay, fe.TimeOfDay));
                }

                days.Add(new NotaryDayAvailabilityDto
                {
                    Date = day,
                    FreeSlots = freeSlots
                });
            }

            return days;
        }

        // ---- helpers ----

        private static DateTime AlignToDay(DateOnly day, DateTime dt)
            => new DateTime(day.Year, day.Month, day.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        // Subtract many busy windows from a single window
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
                    if (b.be <= fs || b.bs >= fe) continue; // no overlap

                    free.RemoveAt(i);
                    if (b.bs > fs) free.Insert(i++, (fs, b.bs)); // before
                    if (b.be < fe) free.Insert(i, (b.be, fe)); // after
                }
            }
            return free;
        }

        // Subtract many busy windows from many free windows
        private static List<(DateTime start, DateTime end)> SubtractWindows(
            IEnumerable<(DateTime start, DateTime end)> windows,
            IEnumerable<(DateTime bs, DateTime be)> busy)
        {
            var current = windows.ToList();
            foreach (var b in busy.OrderBy(x => x.bs))
                current = current
                    .SelectMany(w => SubtractWindows(w, new[] { b }))
                    .ToList();

            return current;
        }
    }

    // Simple DateOnly helpers
    internal static class DateOnlyExtensions
    {
        public static DayOfWeek DayOfWeek(this DateOnly d)
            => d.ToDateTime(TimeOnly.MinValue).DayOfWeek;

        public static DateTime ToDateTime(this DateOnly d, TimeSpan time)
            => new DateTime(d.Year, d.Month, d.Day).Add(time);
    }
}
