using FluentValidation;

namespace ProjectAPI.Api.Application.TypeBiens.UpdateTypeBien;

/// <summary>
/// Validator for UpdateTypeBienCommand.
/// </summary>
public class UpdateTypeBienValidator : AbstractValidator<UpdateTypeBienCommand>
{
    public UpdateTypeBienValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");

        RuleFor(x => x.Name)
            .NotEmpty().When(x => x.Name != null).WithMessage("Name cannot be empty when provided.")
            .MaximumLength(100).When(x => x.Name != null).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description != null).WithMessage("Description cannot exceed 500 characters.");

        RuleFor(x => x.Image)
            .MaximumLength(500).When(x => x.Image != null).WithMessage("Image URL cannot exceed 500 characters.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).When(x => x.Price.HasValue).WithMessage("Price must be greater than or equal to 0.");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue).WithMessage("Minimum price must be greater than or equal to 0.");

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue).WithMessage("Maximum price must be greater than or equal to 0.")
            .Must((command, maxPrice) => !command.MinPrice.HasValue || !maxPrice.HasValue || command.MinPrice <= maxPrice)
            .When(x => x.MaxPrice.HasValue && x.MinPrice.HasValue)
            .WithMessage("Maximum price must be greater than or equal to minimum price.");

        RuleFor(x => x.NbrChambre)
            .GreaterThanOrEqualTo(0).When(x => x.NbrChambre.HasValue).WithMessage("Number of bedrooms must be greater than or equal to 0.");

        RuleFor(x => x.NbrSalleDeBain)
            .GreaterThanOrEqualTo(0).When(x => x.NbrSalleDeBain.HasValue).WithMessage("Number of bathrooms must be greater than or equal to 0.");

        RuleFor(x => x.MinSurface)
            .GreaterThanOrEqualTo(0).When(x => x.MinSurface.HasValue).WithMessage("Minimum surface must be greater than or equal to 0.");

        RuleFor(x => x.MaxSurface)
            .GreaterThanOrEqualTo(0).When(x => x.MaxSurface.HasValue).WithMessage("Maximum surface must be greater than or equal to 0.")
            .Must((command, maxSurface) => !command.MinSurface.HasValue || !maxSurface.HasValue || command.MinSurface <= maxSurface)
            .When(x => x.MaxSurface.HasValue && x.MinSurface.HasValue)
            .WithMessage("Maximum surface must be greater than or equal to minimum surface.");

        RuleFor(x => x.ImagesInterieur)
            .MaximumLength(1000).When(x => x.ImagesInterieur != null).WithMessage("Interior images cannot exceed 1000 characters.");
    }
}
