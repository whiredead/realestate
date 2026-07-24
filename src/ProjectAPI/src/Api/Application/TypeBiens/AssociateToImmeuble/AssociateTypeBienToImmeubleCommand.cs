namespace ProjectAPI.Api.Application.TypeBiens.AssociateToImmeuble
{
    /// <summary>
    /// Command to associate an existing TypeBien with an existing Immeuble.
    /// </summary>
    public class AssociateTypeBienToImmeubleCommand : IRequest<AssociateTypeBienToImmeubleResponse>
    {
        public Guid ImmeubleId { get; set; }
        public int TypeBienId { get; set; }
    }

    /// <summary>
    /// Response for associating a TypeBien with an Immeuble.
    /// </summary>
    public class AssociateTypeBienToImmeubleResponse
    {
        public Guid ImmeubleTypeBienId { get; set; }
        public string Message { get; set; }
    }
}
