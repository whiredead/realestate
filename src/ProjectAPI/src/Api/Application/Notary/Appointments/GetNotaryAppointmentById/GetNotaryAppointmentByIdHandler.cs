using ProjectAPI.Domain.Appointments.Interfaces;


namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentById
{
    /// <summary>
    /// Handler for retrieving a notary appointment by ID.
    /// </summary>
    public class GetNotaryAppointmentByIdHandler : IRequestHandler<GetNotaryAppointmentByIdQuery, GetNotaryAppointmentByIdResponse>
    {
        private readonly INotaryAppointmentRepository _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetNotaryAppointmentByIdHandler"/> class.
        /// </summary>
        /// <param name="repository">The repository for notary appointments.</param>
        public GetNotaryAppointmentByIdHandler(INotaryAppointmentRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Handles the query to retrieve a notary appointment by ID.
        /// </summary>
        /// <param name="request">The query containing the appointment ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The appointment details.</returns>
        public async Task<GetNotaryAppointmentByIdResponse> Handle(GetNotaryAppointmentByIdQuery request, CancellationToken cancellationToken)
        {
            var appointment = await _repository.GetByIDAsync(request.Id);

            if (appointment == null)
            {
                throw new Exception($"Notary appointment with ID {request.Id} not found.");
            }

            return new GetNotaryAppointmentByIdResponse
            {
                Id = appointment.Id,
                BuyerId = appointment.BuyerId,
                NotaireId = appointment.NotaireId,
                AgentId = appointment.AgentId,
                ConnectedUserId = appointment.ConnectedUserId,
                ReservationId = appointment.ReservationId,
                AppointmentDate = appointment.AppointmentDate,
                Status = appointment.Status,
                BuyerFirstName = appointment.BuyerFirstName,
                BuyerLastName = appointment.BuyerLastName,
                BuyerCIN = appointment.BuyerCIN,
                BuyerEmail = appointment.BuyerEmail,
                BuyerPhoneNumber = appointment.BuyerPhoneNumber,
                PropertyPrice = appointment.PropertyPrice,
                TaxFees = appointment.TaxFees,
                TahfidFees = appointment.TahfidFees
            };
        }
    }
}
