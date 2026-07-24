using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.FeedBacks.Entities;
using ProjectAPI.Domain.FeedBacks.Interfaces;
using ProjectAPI.Infrastructure.Context;
namespace ProjectAPI.Infrastructure.Repositories;

public class FeedbackRepository :  BaseRepository<Feedback> ,IFeedbackRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedbackRepository"/> class.
    /// </summary>
    /// <param name="context">The database context to be used by this repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when the context is null.</exception>
    public FeedbackRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
}
