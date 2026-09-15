using System.Collections.Concurrent;
using System.Reflection;
using ProjectAPI.Api.Application.Common.Security;

namespace ProjectAPI.Api.Application.Common.Behaviours;

/// <summary>
/// Audit attribution comes from the signed-in caller, never from the request body.
///
/// Many commands carry the acting user as a plain property (ActorUserId,
/// AuthorUserId, CreatedBy…) bound from JSON, and their handlers write it into
/// history, payments and reports. A caller could send someone else's id and the
/// audit trail recorded that person. For an authenticated caller every such
/// property is overwritten with the token's user id before the handler runs.
///
/// Requests without an authenticated user (background jobs, the anonymous
/// invitation flow) are left untouched: they set these fields themselves.
/// </summary>
public class ActorStampingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Properties that name who performed the action. Not "UserId", which on some commands is the target user.</summary>
    private static readonly string[] ActorProperties =
    {
        "ActorUserId", "AuthorUserId", "CreatedBy", "ConnectedUserId", "UpdatedByUserId", "BuyerUserId",
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Cache = new();

    private readonly ICurrentUser _currentUser;

    public ActorStampingBehaviour(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_currentUser.IsAuthenticated && !string.IsNullOrEmpty(_currentUser.UserId))
        {
            var props = Cache.GetOrAdd(request.GetType(), static type => ActorProperties
                .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
                .Where(p => p is not null && p.CanWrite && p.PropertyType == typeof(string))
                .Cast<PropertyInfo>()
                .ToArray());

            foreach (var prop in props)
                prop.SetValue(request, _currentUser.UserId);
        }

        return next();
    }
}
