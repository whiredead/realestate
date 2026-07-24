namespace ProjectAPI.Domain.Projects.DTOs
{
    /// <summary>
    /// Data transfer object for an agent.
    /// </summary>
    public class AgentDTO
    {
        /// <summary>
        /// Gets or sets the unique identifier of the agent.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the first name of the agent.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the last name of the agent.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Gets or sets the email of the agent.
        /// </summary>
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
    }
}
