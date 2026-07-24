namespace ProjectAPI.Api.Application.Immeubles.DeleteImmeuble;

/// <summary>
/// Response for the delete immeuble operation.
/// </summary>
public class DeleteImmeubleResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> DeletedEntities { get; set; } = new();
    public List<string> Details { get; set; } = new();
}
