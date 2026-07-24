namespace ProjectAPI.Api.Application.Immeubles.DeleteImmeuble;

/// <summary>
/// Command for deleting a immeuble.
/// </summary>
public class DeleteImmeublesCommand : IRequest<DeleteImmeubleResponse>
{
    /// <summary>
    /// Gets or sets the ID of the immeuble to delete.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteImmeublesCommand"/> class.
    /// </summary>
    /// <param name="id">The ID of the immeuble to delete.</param>
    public DeleteImmeublesCommand(Guid id)
    {
        Id = id;
    }
}
