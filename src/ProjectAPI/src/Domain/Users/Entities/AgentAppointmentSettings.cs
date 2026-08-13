namespace ProjectAPI.Domain.Users.Entities;

/// <summary>
/// Per-agent appointment slot configuration — how long a booked slot occupies
/// the agent's calendar, and how much buffer must separate it from the next
/// one. One row per agent (optional; agents without a row use the defaults in
/// <see cref="DefaultDurationMinutes"/>/<see cref="DefaultBufferMinutes"/> when
/// generating slots).
/// </summary>
public class AgentAppointmentSettings
{
    public const int DefaultDurationMinutes = 30;
    public const int DefaultBufferMinutes = 0;

    public Guid Id { get; set; }
    public string AgentId { get; set; }
    public int DurationMinutes { get; set; } = DefaultDurationMinutes;
    public int BufferMinutes { get; set; } = DefaultBufferMinutes;

    public Agent Agent { get; set; } = null!;
}
