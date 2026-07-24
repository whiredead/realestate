using FluentValidation;

namespace ProjectAPI.Api.Application.TypeBiens.DeleteTypeBien;

/// <summary>
/// Validator for the DeleteTypeBienCommand.
/// </summary>
public class DeleteTypeBienValidator : AbstractValidator<DeleteTypeBienCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTypeBienValidator"/> class.
    /// </summary>
    public DeleteTypeBienValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("TypeBien ID must be greater than 0.");
    }
}