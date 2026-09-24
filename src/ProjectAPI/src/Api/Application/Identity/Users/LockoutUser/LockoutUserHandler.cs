using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;

namespace ProjectAPI.Api.Application.Identity.Users.LockoutUser;

/// <summary>
/// Handler for locking out a user.
/// </summary>
public class LockoutUserHandler : IRequestHandler<LockoutUserQuery, Unit>
{
    private readonly UserManager<User> _userManager;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockoutUserHandler"/> class.
    /// </summary>
    /// <param name="userManager">The user manager to manage users.</param>
    /// <param name="currentUser">The caller, so an admin cannot deactivate their own account.</param>
    public LockoutUserHandler(UserManager<User> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Handles the request to lock out a user.
    /// </summary>
    /// <param name="request">The request object containing the user ID to lockout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<Unit> Handle(LockoutUserQuery request, CancellationToken cancellationToken)
    {
        // Find the user you want to lockout (you need to provide the user's id)
        var user = await _userManager.FindByIdAsync(request.UserId);

        if (user == null)
            throw new NotFoundException("User Not Found!");

        // Deactivation is the platform's "delete": the account and everything that
        // references it stay intact, it simply can no longer sign in, and it can be
        // reactivated (UnlockUser). Two guards keep an admin from locking everyone out.
        if (string.Equals(user.Id, _currentUser.UserId, StringComparison.Ordinal))
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure("UserId", "Vous ne pouvez pas désactiver votre propre compte.")
            });
        }

        var isGlobalAdmin = RoleCodes.Normalize(await _userManager.GetRolesAsync(user)).Contains(RoleCodes.GlobalAdmin);
        if (isGlobalAdmin)
        {
            // Roles may be stored under the spec code or the legacy label.
            var admins = (await _userManager.GetUsersInRoleAsync(RoleCodes.GlobalAdmin))
                .Concat(await _userManager.GetUsersInRoleAsync("Admin"))
                .Where(u => u.Id != user.Id)
                .DistinctBy(u => u.Id);
            var otherActiveAdmin = admins.Any(u => u.LockoutEnd is null || u.LockoutEnd <= DateTimeOffset.UtcNow);
            if (!otherActiveAdmin)
            {
                throw new Common.Exceptions.ValidationException(new[]
                {
                    new ValidationFailure("UserId", "Impossible de désactiver le dernier administrateur global actif.")
                });
            }
        }

        // The end date is only enforced when lockout is enabled for the account.
        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        // Return success
        return Unit.Value;
    }
}