namespace ProjectAPI.Api.Application.Units.GetUnitsByProjectId;

/// <summary>
/// Validator for GetUnitsByProjectIdQuery.
/// </summary>
public class GetUnitsByProjectIdValidator : AbstractValidator<GetUnitsByProjectIdQuery>
{
    public GetUnitsByProjectIdValidator()
    {
        RuleFor(q => q.ImmeubleId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(q => q.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}
