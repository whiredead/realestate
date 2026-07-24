namespace ProjectAPI.Api.Application.Purchases.CreatePurchase
{
    /// <summary>
    /// Command for creating a new purchase.
    /// </summary>
    public class CreatePurchaseCommand : IRequest<CreatePurchaseResponse>
    {
        /// <summary>
        /// Gets or sets the ID of the user who made the purchase.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the sale ID associated with the purchase.
        /// </summary>
        public Guid? SaleId { get; set; }

        /// <summary>
        /// Gets or sets the reservation ID associated with the purchase.
        /// </summary>
        public Guid? ReservationId { get; set; }

        /// <summary>
        /// Gets or sets the notary appointment ID associated with the purchase.
        /// </summary>
        public Guid? NotaryAppointmentId { get; set; }

        /// <summary>
        /// Gets or sets the total price of the property.
        /// </summary>
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Gets or sets the amount already paid.
        /// </summary>
        public decimal PaidAmount { get; set; }

        /// <summary>
        /// Gets or sets the remaining amount to be paid.
        /// </summary>
        public decimal RemainingAmount { get; set; }
    }
}
