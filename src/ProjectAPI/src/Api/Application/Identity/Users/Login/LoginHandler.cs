using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Identity.Interfaces;
using Microsoft.AspNetCore.Identity;
using ProjectAPI.Infrastructure.Context;
using System.Security.Cryptography;

namespace ProjectAPI.Api.Application.Identity.Users.Login;

/// <summary>
/// Handles the login command and generates a response.
/// </summary>
public class LoginHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly SignInManager<User> _signInManager;
    private readonly ITokenProvider _tokenProvider;
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _db;


    public LoginHandler(
        SignInManager<User> signInManager,
        ITokenProvider tokenProvider,
        IUserRepository userRepository, ApplicationDbContext db
       )
    {
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(signInManager));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _db = db;
    }

    /// <summary>
    /// Handles the login command and generates a login response.
    /// </summary>
    /// <param name="request">The LoginCommand request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A LoginResponse containing authentication information.</returns>
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find the user by username
            var user = await _userRepository.GetUserByName(request.UserName);

            // Check if the user exists and the password is correct
            if (user != null)
            {
                // Sign in the user
                var result = await _signInManager.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: false);

                // Check if the sign-in was successful
                if (result.Succeeded)
                {
                    // Generate a JWT token
                    var token = _tokenProvider.GenerateAccessToken(user);
                    var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                    _db.SessionRefreshTokens.Add(new SessionRefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken))), ExpiresAtUtc = DateTime.UtcNow.AddDays(14) });
                    await _db.SaveChangesAsync(cancellationToken);

                    // Adapt user entity to UserResponse model
                    var userResponse = user.Adapt<UserResponse>();

                    // Create the login response
                    var response = new LoginResponse()
                    {
                        User = userResponse,
                        AccessToken = token,
                        RefreshToken = refreshToken,
                        IsAutheticated = true,
                        Message = "Logged In Successfully",
                        // Spec §6.1 codes, not the stored legacy labels: the UI
                        // gates on these, and §6.4 requires one shared role
                        // vocabulary between API and interface.
                        Roles = RoleCodes.Normalize(user.GetRoleNames()).ToList(),
                    };

                    return response;
                }

            }        
            // Return invalid credential if the login is unsuccessful
            return new LoginResponse { AccessToken = null, IsAutheticated = false, Message = "Invalid username or password" };

        }
        catch (Exception ex)
        {
            return new LoginResponse { AccessToken = null, IsAutheticated = false, Message = "Invalid username or password" };

        }
    }
}
