using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Security;

public interface IPermissionService
{
    Task<bool> CanAsync(string resource, string action, CancellationToken ct = default);
    Task<bool> IsManagedAsync(string resource, string action, CancellationToken ct = default);
}

public sealed class PermissionCacheVersion
{
    private long _value;
    public long Value => Interlocked.Read(ref _value);
    public void Advance() => Interlocked.Increment(ref _value);
}

internal sealed record PermissionSnapshot(HashSet<string> Allowed, HashSet<string> Managed);

public sealed class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db; private readonly ICurrentUser _user; private readonly IMemoryCache _cache; private readonly PermissionCacheVersion _version;
    public PermissionService(ApplicationDbContext db, ICurrentUser user, IMemoryCache cache, PermissionCacheVersion version) => (_db, _user, _cache, _version) = (db, user, cache, version);
    public async Task<bool> CanAsync(string resource, string action, CancellationToken ct = default)
        => _user.IsGlobalAdmin || (await SnapshotAsync(ct)).Allowed.Contains(Key(resource, action));
    public async Task<bool> IsManagedAsync(string resource, string action, CancellationToken ct = default)
        => !_user.IsGlobalAdmin && (await SnapshotAsync(ct)).Managed.Contains(Key(resource, action));
    private async Task<PermissionSnapshot> SnapshotAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_user.UserId)) return new PermissionSnapshot([], []);
        var cacheKey = $"permissions:{_version.Value}:{_user.UserId}";
        return (await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var roleRules = await _db.RolePermissions.AsNoTracking().Where(x => _user.Roles.Contains(x.RoleCode)).ToListAsync(ct);
            var userRules = await _db.UserPermissionOverrides.AsNoTracking().Where(x => x.UserId == _user.UserId).ToListAsync(ct);
            var managed = new HashSet<string>(roleRules.Select(x => Key(x.Resource, x.Action)), StringComparer.OrdinalIgnoreCase);
            foreach (var rule in userRules) managed.Add(Key(rule.Resource, rule.Action));
            var allowed = new HashSet<string>(roleRules.Where(x => x.Allowed).Select(x => Key(x.Resource, x.Action)), StringComparer.OrdinalIgnoreCase);
            foreach (var rule in userRules) { var key = Key(rule.Resource, rule.Action); if (rule.Allowed) allowed.Add(key); else allowed.Remove(key); }
            return new PermissionSnapshot(allowed, managed);
        }))!;
    }
    private static string Key(string resource, string action) => $"{resource}:{action}";
}
