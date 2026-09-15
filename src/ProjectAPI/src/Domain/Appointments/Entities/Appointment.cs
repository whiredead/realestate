using ProjectAPI.Domain.Crm.Entities;
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
        /// Gets or sets the unique identifier of the sales agent managing the appointment.
        /// </summary>
        public string? SalesAgentId { get; set; }

        /// <summary>
        /// How SalesAgentId was chosen: "EXISTING_OWNER" | "ROUND_ROBIN" |
        /// "LOWEST_WORKLOAD" | "PRIMARY_AGENT" | "MANUAL_REASSIGNMENT".
        /// Null for appointments created before this field existed, or if no
        /// agent has ever been assigned. This and the four fields below hold
        /// only the CURRENT assignment — every assignment/reassignment event,
        /// including this one, is also appended to
        /// <see cref="AssignmentHistory"/>, which a single PreviousSalesAgentId
        /// cannot retain across more than one reassignment.
        /// </summary>
        public string? AssignmentSource { get; set; }

        /// <summary>When the current SalesAgentId was assigned.</summary>
        public DateTime? AssignedAt { get; set; }

        /// <summary>Who caused the current assignment: the acting admin/agent, or null for a system-driven automatic assignment.</summary>
        public string? AssignedByUserId { get; set; }

        /// <summary>The agent this appointment was assigned to immediately before the current one. Null if this is the first assignment.</summary>
        public string? PreviousSalesAgentId { get; set; }

        /// <summary>Why the current assignment replaced a previous one. Null for a first-time (non-reassignment) assignment.</summary>
        public string? ReassignmentReason { get; set; }

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
        /// §1.1 — the CRM contact this request belongs to, resolved or created via
        /// <see cref="Api.Application.Common.Crm.IContactResolver"/>. A visitor
        /// with no account still gets exactly one contact.
        /// </summary>
        public Guid? CrmContactId { get; set; }
        public CrmContact? CrmContact { get; set; }

        /// <summary>
        /// §5.1/§10.3 — when this attempt follows a reschedule request the
        /// requester refused, or a retry after a terminal status, this points at
        /// the appointment it replaces. History is never rewritten in place.
        /// </summary>
        public Guid? PreviousAppointmentId { get; set; }

        /// <summary>
        /// Gets or sets the property type as a string.
        /// </summary>
        public string? PropertyType { get; set; }

        /// <summary>
        /// Gets or sets the overall status of the appointment (§47.3 shared
        /// machine, stored as its canonical string via
        /// <see cref="AppointmentAttemptStatus"/>).
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
        /// The visitor's message from the visit request form (and the plan they were looking at).
        /// </summary>
        public string? Notes { get; set; }

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

        /// <summary>Append-only log of every agent assignment/reassignment on this appointment.</summary>
        public ICollection<AppointmentAssignmentHistory> AssignmentHistory { get; set; } = new List<AppointmentAssignmentHistory>();

        /// <summary>The sales agent's follow-up report(s) after the visit — versioned, see AppointmentVisitReport.</summary>
        public ICollection<AppointmentVisitReport> VisitReports { get; set; } = new List<AppointmentVisitReport>();

        /// <summary>
        /// Gets or sets the list of TypeBien IDs selected for this appointment.
        /// These IDs correspond to types of properties the user is interested in.
        /// </summary>
        public List<int> TypeBienIds { get; set; } = new List<int>();

        /// <summary>When this row was created. Existing rows are backfilled from AppointmentDate (best available approximation).</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
