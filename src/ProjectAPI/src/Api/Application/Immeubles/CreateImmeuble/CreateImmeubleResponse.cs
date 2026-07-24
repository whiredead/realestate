namespace ProjectAPI.Api.Application.Immeubles.CreateImmeuble
{
    /// <summary>
    /// Represents the response for the creation of a immeuble.
    /// </summary>
    public class CreateImmeubleResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier of the created immeuble.
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Gets or sets the message indicating the status of the immeuble creation.
        /// </summary>
        public string Message { get; set; }
    }
}