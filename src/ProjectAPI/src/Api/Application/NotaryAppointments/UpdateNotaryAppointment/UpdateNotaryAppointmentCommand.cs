namespace ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;

public class UpdateNotaryAppointmentCommand : IRequest<UpdateNotaryAppointmentResponse>
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
    public decimal? TaxFees { get; set; }
    public decimal? TahfidFees { get; set; }
}