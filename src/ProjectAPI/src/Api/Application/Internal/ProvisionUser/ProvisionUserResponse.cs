namespace ProjectAPI.Api.Application.Internal.ProvisionUser;

public class ProvisionUserResponse
{
    public required string Id { get; init; }
    public required bool AlreadyExisted { get; init; }
}
