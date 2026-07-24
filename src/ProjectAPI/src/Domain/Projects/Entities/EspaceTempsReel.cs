namespace ProjectAPI.Domain.Projects.Entities
{
    /// <summary>
    /// Represents real-time video entries showing the progress of a project.
    /// </summary>
    public class EspaceTempsReel
    {
        /// <summary>
        /// Gets or sets the unique identifier for this entry.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the video URL or identifier (could be a string link, YouTube ID, or other format).
        /// </summary>
        public string VideoLink { get; set; }

        /// <summary>
        /// Gets or sets the date and time this video was inserted/uploaded.
        /// </summary>
        public DateTime InsertedAt { get; set; }

        /// <summary>
        /// Gets or sets the ID of the associated project.
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Navigation property to the related project.
        /// </summary>
        public Project Project { get; set; }
    }
}
