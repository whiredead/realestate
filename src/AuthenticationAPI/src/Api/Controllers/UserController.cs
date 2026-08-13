using AuthenticationAPI.Api.Application.Common.Models;
using AuthenticationAPI.Api.Application.Roles.GetAllRoles;
using AuthenticationAPI.Api.Application.Users.AdminChangePassword;
using AuthenticationAPI.Api.Application.Users.ConfirmEmail;
using AuthenticationAPI.Api.Application.Users.CreateUserByAdmin;
using AuthenticationAPI.Api.Application.Users.DeleteUser;
using AuthenticationAPI.Api.Application.Users.ForgotPassword;
using AuthenticationAPI.Api.Application.Users.GetAllUsers;
using AuthenticationAPI.Api.Application.Users.GetUsersByIds;
using AuthenticationAPI.Api.Application.Users.GetUsersByRole;
using AuthenticationAPI.Api.Application.Users.LockoutUser;
using AuthenticationAPI.Api.Application.Users.Login;
using AuthenticationAPI.Api.Application.Users.Register;
using AuthenticationAPI.Api.Application.Users.ResetPassword;
using AuthenticationAPI.Api.Application.Users.UnlockUser;
using AuthenticationAPI.Api.Application.Users.UpdateUser;
using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.AspNetCore.Authorization;

namespace AuthenticationAPI.Api.Controllers;


[Route("api/[controller]")]
[ApiController]
[Authorize] // Fail closed: every action requires auth unless it opts out below.
public class UserController : ControllerBase
{
    // User account management (list/edit/lock/unlock/delete) is GLOBAL_ADMIN
    // only — a PROJECT_ADMIN manages project staffing via ProjectMembership
    // (ProjectAPI), never account records directly.
    private const string Admins = RoleCodes.GlobalAdmin;

    private readonly ISender _mediator;
    /// <summary>
    /// Constructor for AuthController.
    /// </summary>
    /// <param name="mediator">The mediator for handling commands and queries.</param>
    public UserController(ISender mediator)
    {
        _mediator = mediator;
    }
    /// <summary>
    /// Logs in a user.
    /// </summary>
    /// <param name="command">The login command.</param>
    /// <returns>Returns a token.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }catch(Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
            return BadRequest(ex.Message);
        }

    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="command">The register command.</param>
    /// <returns>Returns a message.</returns>
    [HttpPost]
    [AllowAnonymous] // §6.2 public signup; the handler forbids self-assigning an internal role.
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(Register), new { Id = result });
    }

    /// <summary>
    /// Creates an account directly, for any role — GLOBAL_ADMIN only. Unlike
    /// <see cref="Register"/> (public, PROSPECT-only), this can mint an
    /// internal-role account or a BUYER in one call, with a password the
    /// admin sets. See <see cref="CreateUserByAdminCommand"/> for how this
    /// relates to the invitation flow.
    /// </summary>
    [HttpPost("admin-create")]
    [Authorize(Roles = Admins)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUserByAdmin([FromBody] CreateUserByAdminCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(CreateUserByAdmin), new { id = result.UserId }, result);
    }

    /// <summary>
    /// Confirms a user's email.
    /// </summary>
    /// <param name="token">The command containing the confirmation token.</param>
    /// <param name="userId">The command containing the user ID.</param>
    /// <returns>A response indicating the result of the email confirmation.</returns>
    [HttpPost("confirm-email")]
    [AllowAnonymous] // §6.2 — clicked from the verification email, before any session exists.
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string userId)
    {
        var command = new ConfirmEmailCommand
        {
            Token = token,
            UserId = userId
        };
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <returns>Returns a list of users.</returns>
    [HttpGet]
    [Authorize(Roles = Admins)] // §6.3 "Utilisateurs internes" — admins only.
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UserResponse>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] string? roleId, [FromQuery] double? rating,
        [FromQuery] string? userName,[FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10 )
    {
        var query = new GetAllUsersQuery(roleId, rating, pageNumber, pageSize, userName);
        var users = await _mediator.Send(query);
        return Ok(users);
    }

    /// <summary>
    /// Gets users by their role and assigned BCH ID.
    /// </summary>
    /// <param name="role">The role of the users to retrieve.</param>
    /// <returns>Returns users based on the specified role and assigned BCH ID.</returns>
    [HttpGet("by-role/{role}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersByRole([FromRoute] string role)
    {
        var query = new GetUsersByRoleQuery(role);
        var users = await _mediator.Send(query);
        return Ok(users);
    }

    /// <summary>
    /// Gets users by their Ids .
    /// </summary>
    /// <param name="Ids">The Ids of the users to retrieve.</param>
    /// <returns>Returns users based on the specified Ids.</returns>
    [HttpGet("multiple")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersByIds([FromQuery] string[] Ids)
    {
        var query = new GetUsersByIdsQuery(Ids);
        var users = await _mediator.Send(query);
        return Ok(users);
    }

    /// <summary>
    /// Retrieves all roles.
    /// </summary>
    /// <returns>An action result containing the roles.</returns>
    [HttpGet("roles")]
    public async Task<IActionResult> GetAllRoles()
    {
        var query = new GetAllRolesQuery();
        var roles = await _mediator.Send(query);
        return Ok(roles);
    }

    /// <summary>
    /// Locks out a user by their ID.
    /// </summary>
    /// <param name="id">The ID of the user to lock out.</param>
    /// <returns>An action result indicating success.</returns>
    [HttpGet("lockout/{id}")]
    [Authorize(Roles = Admins)] // §6.4 — suspending an account is an admin action.
    public async Task<IActionResult> LockoutUser([FromRoute] string id)
    {
        var query = new LockoutUserQuery(id);
        var res = await _mediator.Send(query);
        return Ok(res);
    }

    /// <summary>
    /// Unlocks a user by their ID.
    /// </summary>
    /// <param name="id">The ID of the user to unlock.</param>
    /// <returns>An action result indicating success.</returns>
    [HttpGet("unlock/{id}")]
    [Authorize(Roles = Admins)]
    public async Task<IActionResult> UnlockUser([FromRoute] string id)
    {
        var query = new UnlockUserQuery(id);
        var res = await _mediator.Send(query);
        return Ok(res);
    }

    /// <summary>
    /// First half of the forgot-password flow — issues and emails a reset
    /// token. Always returns 200 regardless of whether the email has an
    /// account, so this cannot be used to enumerate registered emails.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous] // The caller has no session at this point.
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await _mediator.Send(command);
        return Ok();
    }

    /// <summary>
    /// Completes a forgot-password reset. Requires the token from
    /// <see cref="ForgotPassword"/> — previously this accepted a bare UserId
    /// with no token at all, so anyone could reset any account's password
    /// without ever receiving the reset email; that gap is closed.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous] // The caller has no session at this point; the token is what proves legitimacy.
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var res = await _mediator.Send(command);
        return Ok(res);
    }

    /// <summary>
    /// GLOBAL_ADMIN sets a new password on any account directly — no reset
    /// token needed, the admin's own authenticated session is the
    /// authorization. See <see cref="AdminChangePasswordCommand"/>.
    /// </summary>
    [HttpPost("admin-change-password")]
    [Authorize(Roles = Admins)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminChangePassword([FromBody] AdminChangePasswordCommand command)
    {
        await _mediator.Send(command);
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Admins)] // §6.4 — a project admin cannot delete accounts; only admins here.
    public async Task<IActionResult> DeleteUser([FromRoute] string id)
    {
        var command = new DeleteUserCommand { UserId = id };
        var response = await _mediator.Send(command);
        
        if (!response.Success)
            return BadRequest(response);
            
        return Ok(response);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Admins)] // Editing an arbitrary account by id is an admin action (no self-scope check yet).
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser([FromRoute] string id, [FromBody] UpdateUserCommand command)
    {
        command.UserId = id;
        var response = await _mediator.Send(command);
        
        if (!response.Success)
            return BadRequest(response);
            
        return Ok(response);
    }
}
