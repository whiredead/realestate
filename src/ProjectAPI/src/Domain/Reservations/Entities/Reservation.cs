using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Reservations.Entities
{
    /// <summary>
    /// Represents a reservation for a property/unit.
    /// </summary>
    public class Reservation
    {
        public Guid Id { get; set; }

        // Buyer Information
        //
        // §1.1/§6.2 — PrimaryContactId is the buyer's identity; the fields below
        // it are a snapshot kept for the legacy read paths. The contact is the
        // person (with or without a login), BuyerId is the optional account.
        // Before CrmContacts existed these inline fields WERE the identity, which
        // is why the same human appeared as four unlinked copies across
        // reservations, appointments, claims and sales.
        public Guid? PrimaryContactId { get; set; }
        public Crm.Entities.CrmContact? PrimaryContact { get; set; }

        public string? BuyerId { get; set; } // If the buyer is a registered user
        public string? Name { get; set; } // For unregistered buyer
        public string? LastName { get; set; }
        public string? CIN { get; set; } // National ID for Morocco
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }

        // Reservation Details
        public Guid UnitId { get; set; }
        public string? UnitDetails { get; set; } // JSON or formatted string with unit info
        public string? AgentId { get; set; } // The agent who made the reservation

        /// <summary>
        /// §5.3 — "owner_sales_agent_id is frozen at submit". AgentId above is
        /// never reassigned by any handler, so it already plays this role; this
        /// field is a copy taken at submit time to make that freeze explicit and
        /// independent of AgentId's meaning ever changing.
        /// </summary>
        public string? OwnerSalesAgentId { get; set; }

        public string? NotaireId { get; set; } // The notary assigned to this reservation
        public decimal TotalPropertyPrice { get; set; } // Total price of the property
        public decimal ReservationAmount { get; set; } // Amount paid during reservation

        /// <summary>§5.3 — the catalogue price at submit time, before any discount.</summary>
        public decimal? CatalogPrice { get; set; }

        /// <summary>§5.3 — discount granted, if any. Requires admin approval above the agent's cap.</summary>
        public decimal Discount { get; set; }

        /// <summary>
        /// §5.3 — "final_price = catalog_price - discount", a snapshot taken at
        /// submit and never recomputed afterward, even if the unit's catalogue
        /// price changes later.
        /// </summary>
        public decimal? FinalPrice { get; set; }

        public DateTime ReservationDate { get; set; }
        public bool IsUnderConstruction { get; set; } // Whether the property is under construction

        // Navigation properties
        public Unit Unit { get; set; }

        /// <summary>
        /// §5.3/§6.2 — co-buyers on this file, each a distinct CrmContact with an
        /// ownership percentage. The primary buyer (PrimaryContactId above) is
        /// not repeated here; percentages across primary + co-buyers must total
        /// 100% when any are provided.
        /// </summary>
        public ICollection<ReservationBuyer> Buyers { get; set; } = new List<ReservationBuyer>();
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ValidatedAt { get; set; }
        public string? ValidatedBy { get; set; }   // admin user id
        public string? AdminNote { get; set; }

        /// <summary>
        /// Deadline after which a SUBMITTED/CHANGES_REQUESTED reservation expires
        /// and releases its unit (spec §12.3). Null means no expiry configured.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        // NEW: uploaded docs after validation
        public ICollection<ReservationDocument> Documents { get; set; } = new List<ReservationDocument>();

        /// <summary>
        /// §31.6 — optimistic concurrency token. Two admins acting on the same
        /// reservation concurrently (e.g. approve + request-changes) must not
        /// silently last-write-wins; a stale write throws
        /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>,
        /// translated to 409 RESOURCE_VERSION_CONFLICT.
        /// </summary>
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

