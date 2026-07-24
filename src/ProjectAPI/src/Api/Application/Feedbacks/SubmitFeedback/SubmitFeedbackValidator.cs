namespace ProjectAPI.Api.Application.Feedbacks.SubmitFeedback;

/// <summary>
/// Validator for the <see cref="SubmitFeedbackCommand"/> class.
/// </summary>
public class SubmitFeedbackValidator : AbstractValidator<SubmitFeedbackCommand>
{
    public SubmitFeedbackValidator()
    {
        RuleFor(command => command.UserId).NotEmpty().WithMessage("User ID is required.");
        RuleFor(command => command.Rating).InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");
        RuleFor(command => command.Comments).MaximumLength(1000).WithMessage("Comments must not exceed 1000 characters.");
    }
}
