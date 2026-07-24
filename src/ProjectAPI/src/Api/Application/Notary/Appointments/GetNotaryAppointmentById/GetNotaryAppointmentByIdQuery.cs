namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentById
{
    /// <summary>
    /// Query to retrieve a notary appointment by ID.
    /// </summary>
    public class GetNotaryAppointmentByIdQuery : IRequest<GetNotaryAppointmentByIdResponse>
    {
        /// <summary>
        /// Gets or sets the ID of the notary appointment.
        /// </summary>
        public Guid Id { get; set; }
    }
}
