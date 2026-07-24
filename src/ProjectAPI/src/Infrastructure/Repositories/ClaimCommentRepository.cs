using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class ClaimCommentRepository(ApplicationDbContext context) : BaseRepository<ClaimComment>(context), IClaimCommentRepository
{
}

