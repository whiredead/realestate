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
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
