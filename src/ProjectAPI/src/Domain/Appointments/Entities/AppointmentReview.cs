namespace ProjectAPI.Domain.Appointments.Entities;

/// <summary>
/// Represents a review provided by a client for a specific real estate appointment.
/// </summary>
public class AppointmentReview
{
    /// <summary>
    /// Gets or sets the unique identifier for the appointment review.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the appointment being reviewed.
    /// </summary>
    public Guid AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the user providing the review.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the overall rating provided by the client (e.g., 1 to 5 stars).
    /// </summary>
    public int OverallRating { get; set; }

    /// <summary>
    /// Gets or sets the rating for agent professionalism.
    /// </summary>
    public int ProfessionalismRating { get; set; }

    /// <summary>
    /// Gets or sets the rating for communication quality.
    /// </summary>
    public int CommunicationRating { get; set; }

    /// <summary>
    /// Gets or sets the rating for punctuality.
    /// </summary>
    public int PunctualityRating { get; set; }

    /// <summary>
    /// Gets or sets the rating for property knowledge.
    /// </summary>
    public int PropertyKnowledgeRating { get; set; }

    /// <summary>
    /// Gets or sets the comments provided by the client regarding the appointment.
    /// </summary>
    public string Comments { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the review was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the name of the client providing the review.
    /// </summary>
    public string ClientName { get; set; }

    /// <summary>
    /// Gets or sets the email of the client providing the review.
    /// </summary>
    public string ClientEmail { get; set; }

    /// <summary>
    /// Gets or sets the overall experience feedback, such as any issues, recommendations, or additional remarks.
    /// </summary>
    public string ExperienceFeedback { get; set; }

    /// <summary>
    /// Navigation property to the related appointment.
    /// </summary>
    public Appointment Appointment { get; set; }

    /// <summary>
    /// Fills the client information if the user is authenticated.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="clientName">The name of the authenticated user.</param>
    /// <param name="clientEmail">The email of the authenticated user.</param>
    public void FillClientInfoIfAuthenticated(string userId, string clientName, string clientEmail)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            UserId = userId;
            ClientName = clientName;
            ClientEmail = clientEmail;
        }
    }
}