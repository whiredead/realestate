namespace ProjectAPI.Api.Application.TypeBiens.AssociateToProject
{
    /// <summary>
    /// Command to associate an existing TypeBien with an existing Project.
    /// </summary>
    public class AssociateTypeBienToProjectCommand : IRequest<AssociateTypeBienToProjectResponse>
    {
        public Guid ProjectId { get; set; }
        public int TypeBienId { get; set; }
    }
    /// <summary>
    /// Response for associating a TypeBien with a Project.
    /// </summary>
    public class AssociateTypeBienToProjectResponse
    {
        public Guid ProjectTypeBienId { get; set; }
        public string Message { get; set; }
    }
}