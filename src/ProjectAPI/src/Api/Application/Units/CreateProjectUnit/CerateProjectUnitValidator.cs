namespace ProjectAPI.Api.Application.Units.CreateProjectUnit;

/// <summary>
/// Validator for the <see cref="CreateProjectUnitCommand"/> class.
/// </summary>
public class CreateProjectUnitValidator : AbstractValidator<CreateProjectUnitCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProjectUnitValidator"/> class.
    /// </summary>
    public CreateProjectUnitValidator()
    {
        RuleFor(command => command.Floor)
            .NotEmpty()
            .WithMessage("Floor number is required.")
            .MaximumLength(50)
            .WithMessage("Floor number must not exceed 50 characters.");

        RuleFor(command => command.UnitNumber)
            .NotEmpty()
            .WithMessage("Unit number is required.")
            .MaximumLength(100)
            .WithMessage("Unit number must not exceed 100 characters.");

        RuleFor(command => command.NumberOfBedrooms)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Number of bedrooms must be at least 0.");

        RuleFor(command => command.NumberOfBathrooms)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Number of bathrooms must be at least 0.");

        RuleFor(command => command.ApartmentSurface)
            .GreaterThan(0)
            .WithMessage("Apartment surface must be greater than 0.");

        // Uncomment to validate the price if required
        // RuleFor(command => command.Price)
        //     .GreaterThan(0)
        //     .WithMessage("Price must be greater than 0.");
    }
}