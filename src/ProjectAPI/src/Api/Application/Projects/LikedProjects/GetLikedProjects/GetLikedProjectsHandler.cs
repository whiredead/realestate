using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Projects.LikedProjects.GetLikedProjects;

public class GetLikedProjectsHandler : IRequestHandler<GetLikedProjectsQuery, PaginatedResponse<LikedProjectResponse>>
{
    private readonly ILikedProjectRepository _repository;
    private readonly ApplicationDbContext _context;
    public GetLikedProjectsHandler(ILikedProjectRepository repository, ApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

public async Task<PaginatedResponse<LikedProjectResponse>> Handle(GetLikedProjectsQuery request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.UserId))
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
            if (!userExists)
            {
                return new PaginatedResponse<LikedProjectResponse>(new List<LikedProjectResponse>(), request.PageNumber, request.PageSize, 0);
            }
        }

        var items = await _repository.Find(
            lp => (string.IsNullOrEmpty(request.UserId) || lp.UserId == request.UserId)
            && (request.ProjectId == null || lp.ProjectId.ToString() == request.ProjectId),
                lp => lp.Project
            );

        var users = _context.Users
            .Where(u => items.Select(lp => lp.UserId).Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName })
            .ToDictionary(u => u.Id);

        var totalItems = items.Count();

        var paginatedData = items
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
.Select(lp => new LikedProjectResponse
            {
                Id = lp.Id,
                UserId = lp.UserId,
                UserName = users.TryGetValue(lp.UserId, out var fetchedUser) ? fetchedUser.UserName : null,
                FirstName = users.TryGetValue(lp.UserId, out fetchedUser) ? fetchedUser.FirstName : null,
                LastName = users.TryGetValue(lp.UserId, out fetchedUser) ? fetchedUser.LastName : null,
                ProjectId = lp.ProjectId,
                ProjectName = lp.Project?.Name,
                ProjectLocation = lp.Project?.Location,
                LikedAt = lp.LikedAt
            })
            .ToList();

        return new PaginatedResponse<LikedProjectResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
    }
