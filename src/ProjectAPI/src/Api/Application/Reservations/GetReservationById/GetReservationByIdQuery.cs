namespace ProjectAPI.Api.Application.Reservations.GetReservationById;
public class GetReservationByIdQuery : IRequest<GetReservationByIdResponse>
{ 
    public Guid? ReservationId { get; set; }
}
