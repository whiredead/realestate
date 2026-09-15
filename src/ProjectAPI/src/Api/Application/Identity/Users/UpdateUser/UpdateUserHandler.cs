using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ProjectAPI.Api.Application.Identity.Users.UpdateUser;

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, UpdateUserResponse>
{
    private readonly UserManager<User> _userManager;

    public UpdateUserHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UpdateUserResponse> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {request.UserId} not found.");

        var hasChanges = false;
        var errors = new List<string>();

        if (!string.IsNullOrEmpty(request.FirstName) && request.FirstName != user.FirstName)
        {
            user.FirstName = request.FirstName;
            hasChanges = true;
        }

        if (!string.IsNullOrEmpty(request.LastName) && request.LastName != user.LastName)
        {
            user.LastName = request.LastName;
            hasChanges = true;
        }

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            var emailExists = await _userManager.FindByEmailAsync(request.Email);
            if (emailExists != null && emailExists.Id != request.UserId)
            {
                errors.Add("Email is already in use by another user.");
            }
            else
            {
                user.Email = request.Email;
                user.UserName = request.Email;
                hasChanges = true;
            }
        }

        if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != user.PhoneNumber)
        {
            user.PhoneNumber = request.PhoneNumber;
            hasChanges = true;
        }

        if (!string.IsNullOrEmpty(request.FirstNameAr))
        {
            user.FirstNameAr = request.FirstNameAr;
            hasChanges = true;
        }

        if (!string.IsNullOrEmpty(request.LastNameAr))
        {
            user.LastNameAr = request.LastNameAr;
            hasChanges = true;
        }

        // About is an agent profile field, not something every account has.
        // Silently ignoring it for a non-agent would let an admin "save" a bio
        // that never appears anywhere, so it is reported as a validation error.
        if (!string.IsNullOrEmpty(request.About))
        {
            if (user is Agent agent)
            {
                agent.About = request.About;
                hasChanges = true;
            }
            else
            {
                errors.Add("Seul un compte agent peut avoir une description.");
            }
        }

        if (errors.Any())
        {
            return new UpdateUserResponse
            {
                Success = false,
                Message = string.Join(", ", errors)
            };
        }

        if (!hasChanges)
        {
            return new UpdateUserResponse
            {
                Success = false,
                Message = "No changes provided."
            };
        }

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return new UpdateUserResponse
            {
                Success = false,
                Message = "Failed to update user: " + string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        return new UpdateUserResponse
        {
            Success = true,
            Message = $"User '{user.Email}' updated successfully."
        };
    }
}