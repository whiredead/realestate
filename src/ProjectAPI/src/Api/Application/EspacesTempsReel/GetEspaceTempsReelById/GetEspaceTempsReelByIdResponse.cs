namespace ProjectAPI.Api.Application.EspacesTempsReel.GetEspaceTempsReelById
{
    /// <summary>
    /// Represents the response for retrieving a single EspaceTempsReel record by ID.
    /// </summary>
    public class GetEspaceTempsReelByIdResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier for this entry.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The video URL or link associated with this real-time entry.
        /// </summary>
        public string VideoLink { get; set; }

        /// <summary>
        /// The timestamp indicating when this video entry was inserted.
        /// </summary>
        public DateTime InsertedAt { get; set; }

        /// <summary>
        /// The ID of the associated project.
        /// </summary>
        public Guid ProjectId { get; set; }
    }
}
