namespace ProjectAPI.Domain.Projects.DTOs;

public class NotaryDTO
{
    /// <summary>
    /// Gets or sets the unique identifier of the notaire.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the first name of the notaire.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name of the notaire.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the email of the notaire.
    /// </summary>
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
}
