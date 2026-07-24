using FluentValidation;

namespace ProjectAPI.Api.Application.Projects.RemoveProject;

public class RemoveProjectValidator : AbstractValidator<RemoveProjectCommand>
{
public RemoveProjectValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty().NotEqual(Guid.Empty).WithMessage("ProjectId is required.");
    }
}