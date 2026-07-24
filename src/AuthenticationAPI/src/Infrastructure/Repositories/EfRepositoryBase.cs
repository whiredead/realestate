using AuthenticationAPI.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Infrastructure.Repositories;

public abstract class EfRepositoryBase<TEntity> where TEntity : class
{
    protected EfRepositoryBase(ApplicationDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Set = Context.Set<TEntity>();
    }

    protected ApplicationDbContext Context { get; }

    protected DbSet<TEntity> Set { get; }

    public virtual async Task InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await Set.AddAsync(entity, cancellationToken);
    }

    public virtual Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return Context.SaveChangesAsync(cancellationToken);
    }
}
