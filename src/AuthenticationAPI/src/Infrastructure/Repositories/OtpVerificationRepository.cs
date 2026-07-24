using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Domain.ApplicationUser.Interfaces;
using System.Collections.Concurrent;

namespace AuthenticationAPI.Infrastructure.Repositories;

/// <summary>
/// Represents an in-memory repository for phone verification operations on anonymous users.
/// </summary>
public class OtpVerificationRepository : IOtpVerificationRepository
{
    private readonly ConcurrentDictionary<string, OtpVerification> _items = new(StringComparer.Ordinal);

    public Task<IEnumerable<OtpVerification>> GetItemsAsync(
        Func<OtpVerification, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        cancellationToken.ThrowIfCancellationRequested();

        var matchingItems = _items.Values.Where(predicate).ToList();
        return Task.FromResult<IEnumerable<OtpVerification>>(matchingItems);
    }

    public Task AddItemAsync(OtpVerification item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<OtpVerification> UpdateItemAsync(
        string id,
        OtpVerification item,
        IReadOnlyCollection<string>? partitionKeys = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        item.Id = id;
        _items[id] = item;
        return Task.FromResult(item);
    }
}
