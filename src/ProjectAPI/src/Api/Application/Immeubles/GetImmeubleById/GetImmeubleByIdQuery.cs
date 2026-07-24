using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Immeubles.GetImmeubleById
{
    /// <summary>
    /// Query for getting a immeuble by its ID.
    /// </summary>
    public class GetImmeubleByIdQuery : IRequest<ImmeubleResponse>
    {
        /// <summary>
        /// Gets or sets the ID of the immeuble.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetImmeubleByIdQuery"/> class.
        /// </summary>
        /// <param name="id">The ID of the immeuble to retrieve.</param>
        public GetImmeubleByIdQuery(Guid id)
        {
            Id = id;
        }
    }

}
