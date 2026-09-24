namespace ProjectAPI.Api.Application.Identity.Users.CreateUserByAdmin;

/// <summary>
/// Same field rules as the public sign-up (<c>RegisterValidator</c>): without
/// them an admin could store a phone number such as "123" that the public form
/// rejects. Password strength is left to ASP.NET Identity's own policy, whose
/// messages the handler already reports.
/// </summary>
public class CreateUserByAdminValidator : AbstractValidator<CreateUserByAdminCommand>
{
    public CreateUserByAdminValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty().WithMessage("Le prénom est requis.")
            .MaximumLength(100).WithMessage("Le prénom ne doit pas dépasser 100 caractères.");
        RuleFor(c => c.LastName).NotEmpty().WithMessage("Le nom est requis.")
            .MaximumLength(100).WithMessage("Le nom ne doit pas dépasser 100 caractères.");
        RuleFor(c => c.Email).NotEmpty().WithMessage("L'email est requis.")
            .EmailAddress().WithMessage("Adresse e-mail invalide.");
        RuleFor(c => c.Password).NotEmpty().WithMessage("Le mot de passe est requis.");
        RuleFor(c => c.PhoneNumber).NotEmpty().WithMessage("Le téléphone est requis.")
            .Matches("^(06|07)\\d{8}$").WithMessage("Format de téléphone invalide : 06 ou 07 suivi de 8 chiffres (ex. 0612345678).");
        RuleFor(c => c.Role).NotEmpty().WithMessage("Le rôle est requis.");
    }
}
