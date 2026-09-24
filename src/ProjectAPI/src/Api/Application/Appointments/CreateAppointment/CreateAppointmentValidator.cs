namespace ProjectAPI.Api.Application.Appointments.CreateAppointment;
public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty().WithMessage("Le projet est obligatoire.");
        RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("Le message ne peut pas dépasser 2000 caractères.");
        //RuleFor(x => x.AgentId).NotEmpty().WithMessage("AgentId is required.");
        RuleFor(x => x.AppointmentDate).GreaterThan(DateTime.Now).WithMessage("La date et l'heure du rendez-vous doivent être dans le futur.");

        // Validation for guests (unauthenticated users)
        When(x => string.IsNullOrEmpty(x.UserId), () =>
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Le prénom est obligatoire.");
            RuleFor(x => x.Email).NotEmpty().WithMessage("L'adresse e-mail est obligatoire.")
                .EmailAddress().WithMessage("Adresse e-mail invalide.");
            RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Le numéro de téléphone est obligatoire.")
                // Visitors may be abroad, so this is looser than the Moroccan-mobile rule of sign-up:
                // digits with an optional +, spaces, dots, dashes and parentheses, 8-20 characters.
                .Matches(@"^[0-9+ ().\-]{8,20}$").WithMessage("Numéro de téléphone invalide (ex. 0612345678).");
        });
    }
}
