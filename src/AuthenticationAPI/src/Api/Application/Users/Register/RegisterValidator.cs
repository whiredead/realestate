using AuthenticationAPI.Domain.ApplicationUser.Entities;

namespace AuthenticationAPI.Api.Application.Users.Register;

/// <summary>
/// Validator for the <see cref="RegisterCommand"/> class.
/// </summary>
public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterValidator"/> class.
    /// </summary>
    public RegisterValidator()
    {
        // Validation rule for UserName
        RuleFor(command => command.UserName).NotEmpty().WithMessage("Username is required.");

        // Validation rule for FirstName
        RuleFor(command => command.FirstName).NotEmpty().WithMessage("French First name is required.");

        // Validation rule for LastName
        RuleFor(command => command.LastName).NotEmpty().WithMessage("French Last name is required.");

        // Arabic names are optional. The spec's bilingual scope is French/English
        // (§7.2, FR-CMS-002); nothing in the §6.2 sign-up flow collects an Arabic
        // name, so requiring one made public registration impossible through the
        // form the spec describes. Kept as a supported field, no longer demanded.

        // Validation rules for Password
        RuleFor(command => command.Password).NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-zA-Z0-9]").WithMessage("Password must contain at least one alphanumeric character.");

        // Validation rules for PhoneNumber
        RuleFor(command => command.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches("^(06|07)\\d{8}$").WithMessage("Invalid phone number format. It should start with 06 or 07 and contain 10 digits.");
        
        // Validation rules for Email
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        // Validation rules for Role
        RuleFor(x => x.Roles)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Roles must not be null")
            .NotEmpty().WithMessage("At least one role is required")
            .Must(roles => roles.TrueForAll(IsValidRole))
            .WithMessage("Invalid role. Allowed: " + string.Join(", ", AllowedRoles));
    }

    /// <summary>
    /// Roles that may be assigned at registration.
    ///
    /// Both vocabularies are accepted: the spec §6.1 codes (the target model)
    /// and the legacy French labels this project shipped with, so existing
    /// clients and seed scripts keep working. <c>RoleCodes.Normalize</c> maps
    /// either onto a single canonical code before any authorisation decision.
    ///
    /// `VISITOR` is intentionally absent — §6.1 defines it as the
    /// *unauthenticated* public user, so it can never be stored on an account.
    ///
    /// `BUYER` (and its legacy label `Acheteur`) is intentionally absent too, for
    /// a different reason: §6.2 makes it a *consequence*, not a choice. The role
    /// is granted to the linked account when a first reservation is approved
    /// (see ProjectAPI `ApproveReservationHandler`). Allowing it here let anyone
    /// self-declare as a buyer at signup, asserting a business fact no one had
    /// approved. Public signup creates a PROSPECT; buyers are made by approval.
    /// </summary>
    private static readonly string[] AllowedRoles =
    {
        // Spec §6.1 codes
        RoleCodes.Prospect, RoleCodes.SalesAgent,
        RoleCodes.Technician, RoleCodes.Notary,
        RoleCodes.ProjectAdmin, RoleCodes.GlobalAdmin,

        // Legacy labels still present in the database and seed scripts
        "Admin", "Agent", "Notaire", "Technicien",
        "AgentBch", "Observer", "SecurityOfficer", "Other"
    };

    /// <summary>
    /// Checks if the provided role is a valid role.
    /// </summary>
    /// <param name="role">The role to validate.</param>
    /// <returns><c>true</c> if the role is valid; otherwise, <c>false</c>.</returns>
    private static bool IsValidRole(string role)
    {
        return AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
