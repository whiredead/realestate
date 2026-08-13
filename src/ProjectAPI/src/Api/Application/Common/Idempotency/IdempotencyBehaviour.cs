using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Common.Idempotency;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Idempotency;

/// <summary>
/// §7 — "Same key + different body ⇒ 409 IDEMPOTENCY_KEY_REUSED." Applies to
/// every <see cref="IIdempotentRequest"/> command in one place instead of
/// hand-rolling the check in each handler.
///
/// First call with a key: runs the handler, stores the response keyed on
/// (operation, key), returns it. Replay with the same key + same body:
/// returns the stored response WITHOUT re-running the handler — this is what
/// makes a network-retried "create reservation"/"record payment" safe. Same
/// key + different body: rejected before the handler runs.
///
/// A key with no completed response yet (a concurrent in-flight call, or a
/// crash between storing the key and finishing the handler) is treated as
/// reusable-once-completed: this behavior does not attempt distributed
/// locking, since the unique index on (Operation, Key) is what actually
/// prevents two concurrent inserts from both succeeding — the loser gets a
/// SQL unique-violation, surfaced as the same 409.
/// </summary>
public class IdempotencyBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ApplicationDbContext _db;

    public IdempotencyBehaviour(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest idempotent || string.IsNullOrWhiteSpace(idempotent.IdempotencyKey))
        {
            return await next();
        }

        var operation = typeof(TRequest).Name;
        var key = idempotent.IdempotencyKey;
        var requestHash = Hash(request);

        var existing = await _db.Set<IdempotencyKeyRecord>()
            .FirstOrDefaultAsync(r => r.Operation == operation && r.Key == key, cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.IdempotencyKeyReused,
                    "Cette clé d'idempotence a déjà été utilisée avec une requête différente.",
                    StatusCodes.Status409Conflict);
            }

            if (existing.CompletedAt is not null && existing.ResponseBody is not null)
            {
                // Replay: the caller retried a call that already succeeded.
                return (TResponse)JsonSerializer.Deserialize(existing.ResponseBody, typeof(TResponse))!;
            }

            // Key recorded but never completed (crash mid-request, or a
            // concurrent call still in flight) — let this attempt proceed;
            // the unique index is the backstop against a true race.
        }
        else
        {
            existing = new IdempotencyKeyRecord
            {
                Id = Guid.NewGuid(),
                Operation = operation,
                Key = key,
                RequestHash = requestHash,
                CreatedAt = DateTime.UtcNow
            };
            _db.Add(existing);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Lost the race to insert the same (Operation, Key): someone
                // else's request is the one that gets to run.
                throw new BusinessRuleException(
                    BusinessErrorCodes.IdempotencyKeyReused,
                    "Cette clé d'idempotence est déjà en cours de traitement.",
                    StatusCodes.Status409Conflict);
            }
        }

        var response = await next();

        existing.ResponseBody = JsonSerializer.Serialize(response, typeof(TResponse));
        existing.ResponseTypeName = typeof(TResponse).AssemblyQualifiedName;
        existing.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return response;
    }

    private static string Hash(TRequest request)
    {
        var json = JsonSerializer.Serialize(request, typeof(TRequest));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
