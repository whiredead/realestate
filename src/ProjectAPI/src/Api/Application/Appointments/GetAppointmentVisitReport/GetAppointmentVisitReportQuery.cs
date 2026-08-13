namespace ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReport;

/// <summary>
/// Full internal view of the latest visit-report version — agent/admin
/// only. For the buyer-facing summary, see GetAppointmentVisitReportForBuyerQuery.
/// </summary>
public class GetAppointmentVisitReportQuery : IRequest<AppointmentVisitReportDto?>
{
    public Guid AppointmentId { get; set; }
}
