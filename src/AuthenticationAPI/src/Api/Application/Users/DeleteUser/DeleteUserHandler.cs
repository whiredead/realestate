using AuthenticationAPI.Api.Application.Common.Exceptions;
using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Api.Application.Users.DeleteUser;

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, DeleteUserResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;

    public DeleteUserHandler(UserManager<User> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<DeleteUserResponse> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {request.UserId} not found.");

        // Check for dependencies within this context
        var dependencies = new List<string>();

        // Check if user has roles assigned
        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Any())
        {
            dependencies.Add($"{userRoles.Count} Role(s): {string.Join(", ", userRoles)}");
        }

        // Note: The user might have dependencies in ProjectAPI's database (Feedbacks, Appointments, etc.)
        // These are in a separate microservice and cannot be checked here.
        // The 500 error is likely caused by FK constraints in the shared database.
        
        // Check common dependencies in the shared AspNetUsers table
        // Since AuthenticationAPI and ProjectAPI likely share the same database,
        // we add a try-catch to handle FK constraint errors gracefully.
        
        if (dependencies.Any())
        {
            return new DeleteUserResponse
            {
                Success = false,
                Message = $"Cannot delete user '{user.UserName}' because of the following dependencies:",
                Dependencies = dependencies
            };
        }

        // Attempt deletion with proper error handling for FK constraints
        try
        {
            var result = await _userManager.DeleteAsync(user);
            
            if (!result.Succeeded)
            {
                return new DeleteUserResponse
                {
                    Success = false,
                    Message = "Failed to delete user: " + string.Join(", ", result.Errors.Select(e => e.Description)),
                    Dependencies = new List<string>()
                };
            }
            
            return new DeleteUserResponse
            {
                Success = true,
                Message = $"User '{user.UserName}' deleted successfully."
            };
        }
        catch (DbUpdateException ex)
        {
            // FK constraint violation - the user has dependencies in other tables
            return new DeleteUserResponse
            {
                Success = false,
                Message = $"Cannot delete user '{user.UserName}' because they have related data in other parts of the system (e.g., Feedbacks, Appointments, Reservations, LikedProjects). Please delete or reassign this data first.",
                Dependencies = new List<string> { "Related data exists in project management system" }
            };
        }
    }
}