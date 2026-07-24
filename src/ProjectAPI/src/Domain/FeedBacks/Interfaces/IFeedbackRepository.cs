using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.FeedBacks.Entities;

namespace ProjectAPI.Domain.FeedBacks.Interfaces;
/// <summary>
 /// Interface for feedback repository operations.
 /// </summary>
public interface IFeedbackRepository : IBaseRepository<Feedback>
{
}

