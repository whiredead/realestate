using ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReport;

namespace ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReportForBuyer;

/// <summary>Buyer-facing summary of the latest visit-report version — never includes InternalNotes, ConfirmedBudget, Objections, or AuthorUserId.</summary>
public class GetAppointmentVisitReportForBuyerQuery : IRequest<AppointmentVisitReportBuyerDto?>
{
    public Guid AppointmentId { get; set; }
}
