using MediatR;

namespace ProjectAPI.Api.Application.Identity.Users.UpdateUser;

public class UpdateUserCommand : IRequest<UpdateUserResponse>
{
    public string UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FirstNameAr { get; set; }
    public string? LastNameAr { get; set; }
    public string? About { get; set; }
}