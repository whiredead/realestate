namespace ProjectAPI.Api.Application.Purchases.CreatePurchase
{
    /// <summary>
    /// Response returned after successfully creating a purchase.
    /// </summary>
    public class CreatePurchaseResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier of the created purchase.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets a message indicating the result of the purchase creation.
        /// </summary>
        public string Message { get; set; }
    }
}
