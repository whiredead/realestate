using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Infrastructure.Context;
using Als.Foundation.Data.EntityFramework;


namespace ProjectAPI.Infrastructure.Repositories;

public class PropertyDeliveryRepository(ApplicationDbContext context) : BaseRepository<PropertyDelivery>(context), IPropertyDeliveryRepository
{
}
