using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Infrastructure.Context;


namespace ProjectAPI.Infrastructure.Repositories;

public class ClaimAttachmentRepository(ApplicationDbContext context) : BaseRepository<ClaimAttachment>(context), IClaimAttachmentRepository
{
}