using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;


namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentById
{
    /// <summary>
    /// Handler for retrieving a notary appointment by ID.
    ///
    /// §6.4 — the controller only gates "is this caller signed in", so ownership
    /// is enforced here: internal roles must be scoped to the appointment's
    /// project, and a buyer may only read their own reservation's appointment.
    /// </summary>
    public class GetNotaryAppointmentByIdHandler : IRequestHandler<GetNotaryAppointmentByIdQuery, GetNotaryAppointmentByIdResponse>
    {
        private readonly INotaryAppointmentRepository _repository;
        private readonly ProjectScopeService _projectScope;
        private readonly ICurrentUser _currentUser;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetNotaryAppointmentByIdHandler"/> class.
        /// </summary>
        /// <param name="repository">The repository for notary appointments.</param>
        /// <param name="projectScope">Enforces the §6.4 project/buyer perimeter.</param>
        /// <param name="currentUser">Enforces the per-notary ownership boundary.</param>
        public GetNotaryAppointmentByIdHandler(INotaryAppointmentRepository repository, ProjectScopeService projectScope, ICurrentUser currentUser)
        {
            _repository = repository;
            _projectScope = projectScope;
            _currentUser = currentUser;
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

            await _projectScope.EnsureReservationAccessAsync(appointment.ReservationId, cancellationToken);
            await _projectScope.EnsureBuyerOwnsReservationAsync(appointment.ReservationId, cancellationToken);

            // Project scope alone let any NOTARY on the project open a
            // colleague's appointment — mirrors the SALES_AGENT ownership
            // check on commercial GetAppointmentByIdHandler.
            if (_currentUser.IsInRole(RoleCodes.Notary) && appointment.NotaireId != _currentUser.UserId)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.Unauthorized,
                    "Ce rendez-vous notarial n'est pas accessible.",
                    StatusCodes.Status403Forbidden);
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
                TahfidFees = appointment.TahfidFees,
                CreatedAt = appointment.CreatedAt,
                PreviousAppointmentId = appointment.PreviousAppointmentId,
                PreviousNotaireId = appointment.PreviousNotaireId,
                ReassignmentReason = appointment.ReassignmentReason
            };
        }
    }
}
