namespace ProjectAPI.Domain.Identity.Entities;
public class SessionRefreshToken { public Guid Id { get; set; } public string UserId { get; set; } = string.Empty; public string TokenHash { get; set; } = string.Empty; public DateTime ExpiresAtUtc { get; set; } public DateTime? RevokedAtUtc { get; set; } }
