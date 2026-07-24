namespace ProjectAPI.Domain.FeedBacks.Entities;
/// <summary>
/// Represents communication between a buyer and an agent.
/// </summary>
public class Communication
{
    public Guid Id { get; set; }
    public string UserId { get; set; } // The user initiating the communication
    public string AgentId { get; set; } // The agent involved
    public string Message { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}
