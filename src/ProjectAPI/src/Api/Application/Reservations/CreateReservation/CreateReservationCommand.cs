using ProjectAPI.Api.Application.Common.Idempotency;

namespace ProjectAPI.Api.Application.Reservations.CreateReservation;

/// <summary>§7 — reservation submission requires an Idempotency-Key: a retried submit must not create two reservations.</summary>
public class CreateReservationCommand : IRequest<CreateReservationResponse>, IIdempotentRequest
{
    public string? IdempotencyKey { get; set; }
    public string? BuyerId { get; set; }

    /// <summary>
    /// An existing prospect (CRM contact) chosen from the list instead of typing a new person. When given, that
    /// contact is used as is, so the same human is never duplicated; their account (if any) is linked as the buyer.
    /// </summary>
    public Guid? ProspectContactId { get; set; }
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? CIN { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid UnitId { get; set; }
    public string? AgentId { get; set; }
    public string? NotaireId { get; set; }
    public decimal TotalPropertyPrice { get; set; }
    public decimal ReservationAmount { get; set; }
    public bool IsUnderConstruction { get; set; }

    /// <summary>
    /// §12.1 — create the file as a DRAFT instead of submitting it immediately.
    ///
    /// A draft does not hold the unit and is not yet subject to the project's
    /// document requirements, which is what makes "attach the documents, then
    /// submit" possible at all: documents are uploaded against a reservation
    /// id, so one has to exist before they can be attached. Default false keeps
    /// the original create-and-submit behaviour for every existing caller.
    /// </summary>
    public bool CreateAsDraft { get; set; }

    /// <summary>§5.3 — discount off the unit's catalogue price, 0 when none.</summary>
    public decimal Discount { get; set; }

    /// <summary>§5.3 — co-buyers, each with an optional ownership percentage (must total 100% with the primary buyer's share, when any are set).</summary>
    public List<CoBuyerInput> CoBuyers { get; set; } = new();

    public class CoBuyerInput
    {
        public string? Name { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Cin { get; set; }
        public decimal? OwnershipPercent { get; set; }
    }
}
