namespace ProjectAPI.Api.Application.Projects.RemoveProject;

public class RemoveProjectResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<string> DeletedEntities { get; set; } = new();
    public List<string> Details { get; set; } = new();
}