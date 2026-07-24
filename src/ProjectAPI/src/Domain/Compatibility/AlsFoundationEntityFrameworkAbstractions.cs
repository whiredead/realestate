namespace Als.Foundation.Data.Abstractions.EntityFramework
{
    public interface IBaseRepository<T> : ProjectAPI.Domain.Common.Interfaces.IBaseRepository<T> where T : class
    {
    }
}
