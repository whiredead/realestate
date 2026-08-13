namespace ProjectAPI.Api.Application.Reservations.DeleteReservationDocument;

public class DeleteReservationDocumentCommand : IRequest<DeleteReservationDocumentResponse>
{
    public Guid DocumentId { get; set; }
}
