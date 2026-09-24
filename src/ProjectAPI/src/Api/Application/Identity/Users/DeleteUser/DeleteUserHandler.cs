using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Identity.Users.LockoutUser;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;

namespace ProjectAPI.Api.Application.Identity.Users.DeleteUser;

/// <summary>
/// "Delete" is a soft delete: the account is deactivated (locked out indefinitely) and can be
/// reactivated with UnlockUser. Rows in AspNetUsers are never removed, so every reservation,
/// appointment or feedback that references the user keeps working, and an account is no longer
/// refused deletion just because it holds a role (every account does).
/// </summary>
public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, DeleteUserResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly ISender _mediator;

    public DeleteUserHandler(UserManager<User> userManager, ISender mediator)
    {
        _userManager = userManager;
        _mediator = mediator;
    }

    public async Task<DeleteUserResponse> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {request.UserId} not found.");

        // Same guards as the explicit deactivate action (not yourself, not the last active global admin).
        await _mediator.Send(new LockoutUserQuery(request.UserId), cancellationToken);

        return new DeleteUserResponse
        {
            Success = true,
            Message = $"Le compte '{user.UserName}' a été désactivé. Il peut être réactivé à tout moment."
        };
    }
}
