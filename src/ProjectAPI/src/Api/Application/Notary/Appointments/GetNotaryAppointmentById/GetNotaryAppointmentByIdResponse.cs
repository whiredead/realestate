namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentById
{
    /// <summary>
    /// Response for the notary appointment retrieval.
    /// </summary>
    public class GetNotaryAppointmentByIdResponse : ProjectAPI.Api.Application.Common.Units.IHasUnitLocation
    {
        public Guid Id { get; set; }
        public string? BuyerId { get; set; }
        public string? NotaireId { get; set; }
        public string? AgentId { get; set; }
        public string? ConnectedUserId { get; set; }
        public Guid ReservationId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; }
        public string? BuyerFirstName { get; set; }
        public string? BuyerLastName { get; set; }
        public string? BuyerCIN { get; set; }
        public string? BuyerEmail { get; set; }
        public string? BuyerPhoneNumber { get; set; }
        public decimal PropertyPrice { get; set; }
        public decimal TaxFees { get; set; }
        public decimal TahfidFees { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? PreviousAppointmentId { get; set; }
        public string? PreviousNotaireId { get; set; }
        public string? ReassignmentReason { get; set; }

        /// <summary>§5.7 result and its note — the detail used to omit both.</summary>
        public string? Outcome { get; set; }
        public string? OutcomeNote { get; set; }

        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public Guid? ImmeubleId { get; set; }
        public string? ImmeubleName { get; set; }
        public string? FloorName { get; set; }
        public string? UnitNumber { get; set; }
        public ProjectAPI.Api.Application.Common.Units.UnitContextDto? UnitContext { get; set; }
    }
}
