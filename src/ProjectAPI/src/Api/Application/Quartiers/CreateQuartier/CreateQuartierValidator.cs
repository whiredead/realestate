namespace ProjectAPI.Api.Application.Quartiers.CreateQuartier;

/// <summary>Limits mirror QuartierConfiguration so an over-long value is a 422, not a database error.</summary>
public class CreateQuartierValidator : AbstractValidator<CreateQuartierCommand>
{
    public CreateQuartierValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Le nom du quartier est obligatoire.")
            .MaximumLength(200).WithMessage("Le nom ne doit pas dépasser 200 caractères.");
        RuleFor(c => c.Description).MaximumLength(2000).WithMessage("La description ne doit pas dépasser 2000 caractères.");
        RuleFor(c => c.Images).MaximumLength(20000).WithMessage("La liste d'images est trop longue.");
    }
}
