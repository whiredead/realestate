using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ProjectAPI.Api.Application.Identity.Users.GetAllUsers;

/// <summary>
/// Handles the request to retrieve all users.
/// </summary>
public class GetAllUsersHandler : IRequestHandler<GetAllUsersQuery, PaginatedResponse<GetAllUsersResponse>>
{
    private readonly ApplicationDbContext _context;

    public GetAllUsersHandler(ApplicationDbContext context)
    {
        _context = context;

    }

    /// <summary>
    /// Handles the request to retrieve all users.
    /// </summary>
    /// <param name="request">The GetAllUsersQuery request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of UserResponse containing information about all users.</returns>
    public async Task<PaginatedResponse<GetAllUsersResponse>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<User> query = _context.Users;

        // Rating lives on Agent, not on the User base type (it is meaningless
        // for a prospect or a notary), so filtering by it also restricts the
        // result to agents — which is what asking for a minimum rating means.
        query = query.Where(u =>
            (request.RoleId == null || u.UserRoles.Any(r => r.Role.Name == request.RoleId)) &&
            (request.UserName == null || u.UserName == request.UserName) &&
            (request.Rating == null || (u is Agent && ((Agent)u).Rating >= request.Rating))
        );

        int totalItems = await query.CountAsync(cancellationToken);

        query = query.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize);

        var usersWithRoles = await query.Select(u => new GetAllUsersResponse
        {
            User = new UserResponse
            {
                Id = u.Id,
                UserName = u.UserName!,
                FirstName = u.FirstName,
                LastName = u.LastName,
                FirstNameAr = u.FirstNameAr,
                LastNameAr = u.LastNameAr,
                Email = u.Email,
                // Agent-only facts; null/0 for every other kind of account.
                Rating = u is Agent ? ((Agent)u).Rating : 0,
                About = u is Agent ? ((Agent)u).About : null,
                PhoneNumber = u.PhoneNumber!,
                LockoutEnd = u.LockoutEnd
            },
            Roles = u.UserRoles.Select(u => u.Role).ToList()
        }).ToListAsync(cancellationToken);

        return new PaginatedResponse<GetAllUsersResponse>(usersWithRoles, request.PageNumber, request.PageSize, totalItems);
    }
}
