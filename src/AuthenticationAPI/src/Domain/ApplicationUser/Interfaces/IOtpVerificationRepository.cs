using AuthenticationAPI.Domain.ApplicationUser.Entities;

namespace AuthenticationAPI.Domain.ApplicationUser.Interfaces;

/// <summary>
/// Represents a repository for verifying phone numbers.
/// </summary>
public interface IOtpVerificationRepository
{
    Task<IEnumerable<OtpVerification>> GetItemsAsync(
        Func<OtpVerification, bool> predicate,
        CancellationToken cancellationToken = default);

    Task AddItemAsync(OtpVerification item, CancellationToken cancellationToken = default);

    Task<OtpVerification> UpdateItemAsync(
        string id,
        OtpVerification item,
        IReadOnlyCollection<string>? partitionKeys = null,
        CancellationToken cancellationToken = default);
}
