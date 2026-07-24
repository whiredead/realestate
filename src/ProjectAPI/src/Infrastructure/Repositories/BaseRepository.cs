using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Infrastructure.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectAPI.Infrastructure.Repositories
{

    public class BaseRepository<T> : IBaseRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _dbContext;
        protected readonly DbSet<T> _dbSet;

        public BaseRepository(ApplicationDbContext context)
        {
            _dbContext = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<T?> GetByIDAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<T> InsertAsync(T entity)
        {
            var entry = await _dbSet.AddAsync(entity);
            await SaveAsync();
            return entry.Entity;
        }

        public virtual async Task Update(T entity)
        {
            _dbSet.Update(entity);
            await SaveAsync();
        }

        public virtual async Task DeleteAsync(object id)
        {
            var entity = await GetByIDAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await SaveAsync();
            }
        }

        public virtual void Delete(T entity)
        {
            _dbSet.Remove(entity);
            _dbContext.SaveChanges();
        }

        public async Task SaveAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
        public virtual async Task<IEnumerable<T>> Find(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }
        public virtual async Task<IEnumerable<T>> Find(System.Linq.Expressions.Expression<Func<T, bool>> predicate, params System.Linq.Expressions.Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet.Where(predicate);
            if (includes != null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }
            return await query.ToListAsync();
        }
        public virtual async Task<IEnumerable<T>> FindWithIncludesAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate,Func<IQueryable<T>, IQueryable<T>> includes)
        {
            IQueryable<T> query = _dbSet.Where(predicate);
            query = includes(query);
            return await query.ToListAsync();
        }
    }
}
