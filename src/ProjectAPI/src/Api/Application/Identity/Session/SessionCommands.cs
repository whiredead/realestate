using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Identity.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Identity.Session;

public sealed record RefreshRequest(string? RefreshToken);
public sealed record SessionTokens(string AccessToken, string RefreshToken);

internal static class RefreshTokenHash
{
    public static string Of(string? token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
}

/// <summary>
/// POST /api/session/refresh — rotates a live refresh token: the presented one is revoked and a new
/// pair is issued. Returns null when the token is unknown, revoked or expired (the caller answers 401).
/// </summary>
public class RefreshSessionCommand : IRequest<SessionTokens?>
{
    public string? RefreshToken { get; set; }
}

public class RefreshSessionHandler : IRequestHandler<RefreshSessionCommand, SessionTokens?>
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ProjectAPI.Domain.Users.Entities.User> _users;
    private readonly ITokenProvider _tokens;

    public RefreshSessionHandler(ApplicationDbContext db, UserManager<ProjectAPI.Domain.Users.Entities.User> users, ITokenProvider tokens)
    {
        _db = db;
        _users = users;
        _tokens = tokens;
    }

    public async Task<SessionTokens?> Handle(RefreshSessionCommand request, CancellationToken ct)
    {
        var hash = RefreshTokenHash.Of(request.RefreshToken);
        var row = await _db.SessionRefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow, ct);
        if (row is null) return null;
        row.RevokedAtUtc = DateTime.UtcNow;

        // Roles must be loaded like at login: FindByIdAsync does not include them, so the refreshed token carried no role claims and every role-gated call answered 403.
        var user = await _users.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
        if (user is null) return null;

        var next = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.SessionRefreshTokens.Add(new SessionRefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = RefreshTokenHash.Of(next), ExpiresAtUtc = DateTime.UtcNow.AddDays(14) });
        await _db.SaveChangesAsync(ct);
        return new SessionTokens(_tokens.GenerateAccessToken(user), next);
    }
}

// POST /api/session/logout — revokes the refresh token; unknown tokens are ignored.
public class LogoutSessionCommand : IRequest<MediatR.Unit>
{
    public string? RefreshToken { get; set; }
}

public class LogoutSessionHandler : IRequestHandler<LogoutSessionCommand, MediatR.Unit>
{
    private readonly ApplicationDbContext _db;
    public LogoutSessionHandler(ApplicationDbContext db) => _db = db;

    public async Task<MediatR.Unit> Handle(LogoutSessionCommand request, CancellationToken ct)
    {
        var hash = RefreshTokenHash.Of(request.RefreshToken);
        var row = await _db.SessionRefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (row is not null)
        {
            row.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return MediatR.Unit.Value;
    }
}
