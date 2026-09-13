using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Reservations.GetReservations
{
    public class GetReservationsQuery : IRequest<PaginatedResponse<GetReservationsResponse>>
    {
        public string? BuyerId { get; set; }
        public string? Name { get; set; }
        public string? LastName { get; set; }
        public string? CIN { get; set; }
        public string? Email { get; set; }
        public Guid? UnitId { get; set; }
        public string? AgentId { get; set; }
        public string? NotaireId { get; set; }
        public bool? IsUnderConstruction { get; set; }

        /// <summary>Optional filter to one project (Reservation -> Unit -> Immeuble -> Project).</summary>
        public Guid? ProjectId { get; set; }

        /// <summary>Optional filter to one building (narrows ProjectId when both are given).</summary>
        public Guid? ImmeubleId { get; set; }

        /// <summary>
        /// Optional status filter, applied server-side BEFORE paging. The console
        /// used to filter the status on the client over a single page, so a tab
        /// showed only the matching rows of that page.
        /// </summary>
        public ProjectAPI.Domain.Reservations.Entities.ReservationStatus? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
