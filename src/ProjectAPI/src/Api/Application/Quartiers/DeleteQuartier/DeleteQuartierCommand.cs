using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Quartiers.DeleteQuartier;

/// <summary>
/// Deletes a quartier that no project references. A referenced quartier is
/// refused (409) rather than silently detaching its projects from the public
/// catalogue: the admin reassigns those projects first.
/// </summary>
public class DeleteQuartierCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
}

public class DeleteQuartierHandler : IRequestHandler<DeleteQuartierCommand, Unit>
{
    private readonly ApplicationDbContext _db;

    public DeleteQuartierHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(DeleteQuartierCommand request, CancellationToken ct)
    {
        var quartier = await _db.Set<Quartier>().FirstOrDefaultAsync(q => q.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quartier {request.Id} not found.");

        var projectCount = await _db.Set<Project>().CountAsync(p => p.QuartierId == request.Id, ct);
        if (projectCount > 0)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Ce quartier est rattaché à {projectCount} projet(s) : rattachez-les à un autre quartier avant de le supprimer.",
                StatusCodes.Status409Conflict);
        }

        _db.Remove(quartier);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
