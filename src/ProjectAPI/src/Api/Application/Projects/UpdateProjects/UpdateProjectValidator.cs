namespace ProjectAPI.Api.Application.Projects.UpdateProjects;

/// <summary>
/// The update path had no validator at all, so the field rules the create path
/// enforces were silently skippable by editing instead of creating.
/// </summary>
public class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        // Name/Location are only checked when supplied: the handler treats null
        // as "leave unchanged", so requiring them here would turn a partial edit
        // into a validation failure.
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Location).NotEmpty().When(x => x.Location is not null);

        RuleFor(x => x.WarrantyMonths)
            .InclusiveBetween(1, 360)
            .When(x => x.WarrantyMonths.HasValue)
            .WithMessage("La durée de garantie doit être comprise entre 1 et 360 mois.");

        RuleFor(x => x.OverallProgress)
            .InclusiveBetween(0m, 100m)
            .When(x => x.OverallProgress.HasValue);
    }
}
