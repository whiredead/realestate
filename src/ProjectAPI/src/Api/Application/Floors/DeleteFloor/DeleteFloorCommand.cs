namespace ProjectAPI.Api.Application.Floors.DeleteFloor;

/// <summary>Deletes a floor — refused (409) while it still holds units, same as every other "can't delete what's still referenced" guard in this codebase.</summary>
public class DeleteFloorCommand : IRequest<MediatR.Unit>
{
    public Guid Id { get; set; }
}
