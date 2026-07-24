using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectAPI.Domain.Common.Interfaces
{
    public interface IBaseRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIDAsync(object id);
        Task<T> InsertAsync(T entity);
        Task Update(T entity);
        Task DeleteAsync(object id);
        void Delete(T entity);
        Task SaveAsync();
        Task<IEnumerable<T>> Find(System.Linq.Expressions.Expression<Func<T, bool>> predicate);
        Task<IEnumerable<T>> Find(System.Linq.Expressions.Expression<Func<T, bool>> predicate, params System.Linq.Expressions.Expression<Func<T, object>>[] includes);
        Task<IEnumerable<T>> FindWithIncludesAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate,Func<IQueryable<T>, IQueryable<T>> includes);

    }
}
