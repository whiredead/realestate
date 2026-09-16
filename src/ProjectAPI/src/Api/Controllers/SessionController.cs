using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Identity.Interfaces;
using ProjectAPI.Infrastructure.Context;
using System.Security.Cryptography;
using System.Text;

namespace ProjectAPI.Api.Controllers;
[ApiController, Route("api/session")]
public sealed class SessionController : ControllerBase
{
 private readonly ApplicationDbContext _db; private readonly UserManager<ProjectAPI.Domain.Users.Entities.User> _users; private readonly ITokenProvider _tokens;
 public SessionController(ApplicationDbContext db, UserManager<ProjectAPI.Domain.Users.Entities.User> users, ITokenProvider tokens) { _db=db; _users=users; _tokens=tokens; }
 [HttpPost("refresh"), AllowAnonymous] public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken ct) { var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.RefreshToken ?? ""))); var row=await _db.SessionRefreshTokens.FirstOrDefaultAsync(x=>x.TokenHash==hash&&x.RevokedAtUtc==null&&x.ExpiresAtUtc>DateTime.UtcNow,ct); if(row is null)return Unauthorized(); row.RevokedAtUtc=DateTime.UtcNow; var user=await _users.FindByIdAsync(row.UserId); if(user is null)return Unauthorized(); var next=Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)); _db.SessionRefreshTokens.Add(new SessionRefreshToken { Id=Guid.NewGuid(),UserId=user.Id,TokenHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(next))),ExpiresAtUtc=DateTime.UtcNow.AddDays(14) }); await _db.SaveChangesAsync(ct); return Ok(new { accessToken=_tokens.GenerateAccessToken(user), refreshToken=next }); }
 [HttpPost("logout"), AllowAnonymous] public async Task<IActionResult> Logout([FromBody] RefreshRequest body, CancellationToken ct) { var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body.RefreshToken ?? ""))); var row=await _db.SessionRefreshTokens.FirstOrDefaultAsync(x=>x.TokenHash==hash,ct); if(row is not null){row.RevokedAtUtc=DateTime.UtcNow;await _db.SaveChangesAsync(ct);} return NoContent(); }
}
public sealed record RefreshRequest(string? RefreshToken);
