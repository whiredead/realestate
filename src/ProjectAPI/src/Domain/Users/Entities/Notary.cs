using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Projects.Entities;
using static System.Reflection.Metadata.BlobBuilder;

namespace ProjectAPI.Domain.Users.Entities;

public class Notary : User
{

    /// <summary>
    /// Gets or sets the collection of appointments associated with the Notary.
    /// </summary>
    public ICollection<NotaryAppointment>? NotaryAppointments { get; set; }


    /// <summary>
    /// Gets or sets the collection of project assignments associated with the Notary.
    /// </summary>

    public ICollection<ProjectAssignment> Assignments { get; set; } = new List<ProjectAssignment>();

    /// <summary>
    /// Gets or sets the collection of disponibility dates associated with the Notary.
    /// </summary>
    public ICollection<NotaryDateDisponibilite>? NotaryDatesDisponibilites { get; set; } = new List<NotaryDateDisponibilite>();
    
    /// <summary>
    /// Gets or sets the collection of disponibility dates associated with the Notary.
    /// </summary>
    public ICollection<WeeklyAvailability>? WeeklyAvailabilities { get; set; } = new List<WeeklyAvailability>();

    /// <summary>
    /// Gets or sets the collection of disponibility dates associated with the Notary.
    /// </summary>
    public ICollection<NotaryBlock>? Blocks { get; set; } = new List<NotaryBlock>();

}