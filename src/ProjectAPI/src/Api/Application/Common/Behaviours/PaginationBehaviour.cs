using System.Collections.Concurrent;
using System.Reflection;
using FluentValidation.Results;
using ValidationException = ProjectAPI.Api.Application.Common.Exceptions.ValidationException;

namespace ProjectAPI.Api.Application.Common.Behaviours;

/// <summary>
/// Guards every paginated query (any request exposing writable int
/// <c>PageNumber</c> and <c>PageSize</c> properties).
///
/// No list query validated its paging parameters: <c>PageNumber=0</c> or a
/// negative <c>PageSize</c> reached <c>Skip(-n)</c> and surfaced as a 500, and an
/// unbounded <c>PageSize</c> let one call load an entire table. Invalid values
/// are now a 422 with field errors; an oversized page is capped at
/// <see cref="MaxPageSize"/> (the response's <c>pageSize</c> reflects the cap, so
/// clients keep paging with <c>totalPages</c>).
/// </summary>
public class PaginationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int MaxPageSize = 500;

    private static readonly ConcurrentDictionary<Type, (PropertyInfo Number, PropertyInfo Size)?> Cache = new();

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var props = Cache.GetOrAdd(request.GetType(), static type =>
        {
            var number = type.GetProperty("PageNumber", BindingFlags.Public | BindingFlags.Instance);
            var size = type.GetProperty("PageSize", BindingFlags.Public | BindingFlags.Instance);
            return number is { PropertyType: var nt, CanWrite: true } && nt == typeof(int)
                && size is { PropertyType: var st, CanWrite: true } && st == typeof(int)
                ? (number, size)
                : null;
        });

        if (props is { } p)
        {
            var pageNumber = (int)p.Number.GetValue(request)!;
            var pageSize = (int)p.Size.GetValue(request)!;
            var failures = new List<ValidationFailure>();
            if (pageNumber < 1) failures.Add(new ValidationFailure("PageNumber", "PageNumber doit être supérieur ou égal à 1."));
            if (pageSize < 1) failures.Add(new ValidationFailure("PageSize", "PageSize doit être supérieur ou égal à 1."));
            if (failures.Count > 0) throw new ValidationException(failures);

            if (pageSize > MaxPageSize) p.Size.SetValue(request, MaxPageSize);
        }

        return next();
    }
}
