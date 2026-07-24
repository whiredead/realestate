using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Appointments.Interfaces;

namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointments;

public class GetNotaryAppointmentsHandler : IRequestHandler<GetNotaryAppointmentsQuery, PaginatedResponse<NotaryAppointmentResponse>>
{
    private readonly INotaryAppointmentRepository _repository;

    public GetNotaryAppointmentsHandler(INotaryAppointmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedResponse<NotaryAppointmentResponse>> Handle(GetNotaryAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var appointments = await _repository.Find(
            na => (request.ReservationId == null || na.ReservationId == request.ReservationId) &&
                  (string.IsNullOrEmpty(request.BuyerCIN) || na.BuyerCIN == request.BuyerCIN) &&
                  (string.IsNullOrEmpty(request.NotaireId) || na.NotaireId == request.NotaireId) &&
                  (string.IsNullOrEmpty(request.AgentId) || na.AgentId == request.AgentId) &&
                  (string.IsNullOrEmpty(request.Status) || na.Status == request.Status)
        );

        var totalItems = appointments.Count();

        var paginatedData = appointments
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(na => new NotaryAppointmentResponse
            {
                Id = na.Id,
                BuyerId = na.BuyerId,
                NotaireId = na.NotaireId,
                AgentId = na.AgentId,
                ReservationId = na.ReservationId,
                AppointmentDate = na.AppointmentDate,
                Status = na.Status,
                PropertyPrice = na.PropertyPrice,
                TaxFees = na.TaxFees,
                TahfidFees = na.TahfidFees
            })
            .ToList();

        return new PaginatedResponse<NotaryAppointmentResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}

