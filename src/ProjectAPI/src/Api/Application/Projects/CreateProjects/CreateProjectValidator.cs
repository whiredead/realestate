namespace ProjectAPI.Api.Application.Projects.CreateProjects;

public class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Location).NotEmpty().WithMessage("Location is required.");
        RuleFor(x => x.Address).NotEmpty().WithMessage("Address is required.");

        // §8 — a warranty of zero months is not "no warranty configured", it is
        // a project whose buyers have no SAV window at all, which is never what
        // an empty form field means. Omit the field to take the default instead.
        RuleFor(x => x.WarrantyMonths)
            .InclusiveBetween(1, 360)
            .When(x => x.WarrantyMonths.HasValue)
            .WithMessage("La durée de garantie doit être comprise entre 1 et 360 mois.");
    }
}
