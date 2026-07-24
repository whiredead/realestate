using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Appointments.Entities
{
    /// <summary>
    /// Represents an appointment related to a project, including details such as project, agent, guest or user information,
    /// and a list of selected TypeBien IDs for which the user wants more information.
    /// </summary>
    public class Appointment
    {
        /// <summary>
        /// Gets or sets the unique identifier for the appointment.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the related project.
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the agent managing the appointment.
        /// </summary>
        public string? AgentId { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the appointment.
        /// </summary>
        public DateTime AppointmentDate { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user associated with the appointment.
        /// If the user is not authenticated, this field may be null.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Gets or sets the property type as a string.
        /// </summary>
        public string? PropertyType { get; set; }

        /// <summary>
        /// Gets or sets the overall status of the appointment.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the name of the guest if the user is not authenticated.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the last name of the guest if the user is not authenticated.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Gets or sets the email of the guest if the user is not authenticated.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the phone number of the guest if the user is not authenticated.
        /// </summary>
        public string PhoneNumber { get; set; }

        /// <summary>
        /// Navigation property to the related project.
        /// </summary>
        public Project Project { get; set; }

        /// <summary>
        /// Navigation property to the agent managing the appointment.
        /// </summary>
        public Agent Agent { get; set; }

        /// <summary>
        /// Gets or sets the collection of reviews for the appointment.
        /// </summary>
        public ICollection<AppointmentReview> Reviews { get; set; }

        /// <summary>
        /// Gets or sets the list of TypeBien IDs selected for this appointment.
        /// These IDs correspond to types of properties the user is interested in.
        /// </summary>
        public List<int> TypeBienIds { get; set; } = new List<int>();
    }
}
