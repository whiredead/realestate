using MediatR;

namespace ProjectAPI.Api.Application.Identity.Users.UpdateUser;

public class UpdateUserCommand : IRequest<UpdateUserResponse>
{
    /// <summary>Taken from the route (PUT /api/User/{id}); optional in the body so model validation does not reject the request before the controller sets it.</summary>
    public string? UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FirstNameAr { get; set; }
    public string? LastNameAr { get; set; }
    public string? About { get; set; }
}