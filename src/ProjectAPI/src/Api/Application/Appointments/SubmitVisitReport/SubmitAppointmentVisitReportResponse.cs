namespace ProjectAPI.Api.Application.Appointments.SubmitVisitReport;

public class SubmitAppointmentVisitReportResponse
{
    public Guid Id { get; set; }
    public int VersionNo { get; set; }
    public string Message { get; set; } = string.Empty;
}
