namespace ProjectAPI.Api.Application.Floors.CreateFloor;

public class CreateFloorValidator : AbstractValidator<CreateFloorCommand>
{
    public CreateFloorValidator()
    {
        RuleFor(c => c.ImmeubleId).NotEmpty().WithMessage("ImmeubleId is required.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(50).WithMessage("Floor name is required and must not exceed 50 characters.");
    }
}
