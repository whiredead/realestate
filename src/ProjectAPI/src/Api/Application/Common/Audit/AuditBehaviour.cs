using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Audit.Entities;
using RoleCodes = ProjectAPI.Domain.Users.Entities.RoleCodes;
using ProjectAPI.Infrastructure.Auditing;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Audit;

/// <summary>
/// §7 audit trail entry point. For an audited command it opens an
/// <see cref="AuditInvocation"/> that the DbContext interceptor uses to write
/// the business row in the command's own transaction — no handler code needed.
///
/// Security events (login, password, OTP…) are written here instead, with their
/// outcome, in a separate scope: a refused login must be recorded even though
/// the command threw and nothing else is saved.
/// </summary>
public class AuditBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditContext _audit;
    private readonly ICurrentUser _user;
    private readonly IHttpContextAccessor _http;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuditBehaviour<TRequest, TResponse>> _logger;

    public AuditBehaviour(IAuditContext audit, ICurrentUser user, IHttpContextAccessor http, IServiceScopeFactory scopes, ILogger<AuditBehaviour<TRequest, TResponse>> logger)
    {
        _audit = audit;
        _user = user;
        _http = http;
        _scopes = scopes;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var type = request.GetType();

        // Consultation of a buyer's personal data: journaled once the read succeeded.
        if (AuditPolicy.Access.TryGetValue(type.Name, out var access))
        {
            var result = await next();
            if (IsStaff()) await WriteAccessEventAsync(request, access);
            return result;
        }

        // A command sent from inside another handler belongs to the outer action.
        if (_audit.Current is not null || !AuditPolicy.IsAudited(type)) return await next();

        var security = AuditPolicy.IsSecurity(type);
        var http = _http.HttpContext;
        var invocation = new AuditInvocation
        {
            Action = AuditPolicy.ActionFor(type),
            Category = security ? AuditCategories.Security : AuditCategories.Business,
            Request = request,
            ActorUserId = _user.UserId,
            ActorLabel = security ? IdentifierOf(request) : null,
            ActorRoles = _user.Roles.Count > 0 ? string.Join(",", _user.Roles) : null,
            RequestId = http?.TraceIdentifier,
            IpAddress = http?.Connection.RemoteIpAddress?.ToString()
        };

        _audit.Begin(invocation);
        try
        {
            var response = await next();
            if (security)
            {
                var outcome = response as IAuditOutcome;
                if (outcome is { Succeeded: false })
                    await WriteSecurityEventAsync(invocation, AuditOutcomes.Failure, outcome.FailureReason);
                else if (!AuditPolicy.SecurityFailuresOnly.Contains(type.Name))
                    await WriteSecurityEventAsync(invocation, AuditOutcomes.Success, null, outcome?.ActorUserId, outcome?.ActorRoles);
            }
            return response;
        }
        catch (Exception ex) when (security)
        {
            await WriteSecurityEventAsync(invocation, AuditOutcomes.Failure, ex.Message);
            throw;
        }
        finally
        {
            _audit.End();
        }
    }

    private async Task WriteSecurityEventAsync(AuditInvocation invocation, string outcome, string? detail, string? resolvedActorUserId = null, string? resolvedActorRoles = null)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Category = invocation.Category,
                Action = invocation.Action,
                Outcome = outcome,
                // A successful login was anonymous when it started: attribute it to the account it opened.
                ActorUserId = invocation.ActorUserId ?? resolvedActorUserId,
                ActorLabel = invocation.ActorLabel,
                ActorRoles = invocation.ActorRoles ?? resolvedActorRoles,
                Detail = detail is { Length: > 500 } ? detail[..500] : detail,
                RequestId = invocation.RequestId,
                IpAddress = invocation.IpAddress
            };
            db.Add(row);

            // The account the identifier designates (a refused login targets someone): list the
            // event in that user's history even though nobody was signed in.
            var targetId = row.ActorUserId;
            if (targetId is null && !string.IsNullOrWhiteSpace(invocation.ActorLabel))
            {
                var normalized = invocation.ActorLabel.ToUpperInvariant();
                targetId = await db.Users.Where(u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized)
                    .Select(u => u.Id).FirstOrDefaultAsync();
            }
            if (targetId is not null)
            {
                row.EntityType = "User";
                row.EntityId = targetId;
                db.Add(new AuditLogSubject { Id = Guid.NewGuid(), AuditLogId = row.Id, EntityType = "User", EntityId = targetId });
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Losing a security event must never turn a successful login into an error.
            _logger.LogError(ex, "Audit: could not record security event {Action}", invocation.Action);
        }
    }

    /// <summary>Only staff reads are consultations; a buyer opening their own file is not.</summary>
    private bool IsStaff() =>
        _user.IsAuthenticated && _user.Roles.Any(r => r is not (RoleCodes.Buyer or RoleCodes.Prospect));

    private async Task WriteAccessEventAsync(object request, AccessSpec access)
    {
        try
        {
            var raw = request.GetType().GetProperty(access.IdProperty)?.GetValue(request);
            if (raw is not Guid id || id == Guid.Empty) return;
            var http = _http.HttpContext;
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Category = AuditCategories.Access,
                Action = access.Action,
                Outcome = AuditOutcomes.Success,
                ActorUserId = _user.UserId,
                ActorRoles = string.Join(",", _user.Roles),
                RequestId = http?.TraceIdentifier,
                IpAddress = http?.Connection.RemoteIpAddress?.ToString()
            };
            await AuditAccessRecorder.RecordAsync(db, row, access.EntityType, id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The document was already served; a lost consultation entry must not turn it into an error.
            _logger.LogError(ex, "Audit: could not record consultation {Action}", access.Action);
        }
    }

    /// <summary>The login/e-mail the caller typed, for events where no account id is known yet. Never the password.</summary>
    private static string? IdentifierOf(object request)
    {
        foreach (var name in new[] { "UserName", "Email", "PhoneNumber" })
        {
            if (request.GetType().GetProperty(name)?.GetValue(request) is string value && !string.IsNullOrWhiteSpace(value))
                return value.Length > 256 ? value[..256] : value;
        }
        return null;
    }
}
