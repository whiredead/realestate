namespace ProjectAPI.Domain.Immeubles.Entities
{
    /// <summary>
    /// Represents tracking information for an Immeuble's status updates.
    /// </summary>
    public class ImmeubleTracking
    {
        /// <summary>
        /// Gets or sets the unique identifier for the tracking record.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the associated Immeuble.
        /// </summary>
        public Guid ImmeubleId { get; set; }

        /// <summary>
        /// Navigation property for the associated Immeuble.
        /// </summary>
        public Immeuble Immeuble { get; set; }

        /// <summary>
        /// Gets or sets the status update for the Immeuble.
        /// </summary>
        public string StatusUpdate { get; set; }

        /// <summary>
        /// Gets or sets the date the status was updated.
        /// </summary>
        public DateTime DateUpdated { get; set; }
    }
}
