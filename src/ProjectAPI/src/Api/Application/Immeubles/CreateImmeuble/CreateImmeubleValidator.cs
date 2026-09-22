namespace ProjectAPI.Api.Application.Immeubles.CreateImmeuble
{
    /// <summary>
    /// Validator for the <see cref="CreateImmeubleCommand"/> class.
    /// </summary>
    public class CreateImmeubleValidator : AbstractValidator<CreateImmeubleCommand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateImmeubleValidator"/> class.
        /// </summary>
        public CreateImmeubleValidator()
        {
            // Validation rule for Name
            RuleFor(project => project.Name)
                .NotEmpty().WithMessage("Project name is required.")
                .MaximumLength(150).WithMessage("Project name must not exceed 150 characters.");

            RuleFor(project => project.Location)
                .MaximumLength(250).WithMessage("La localisation ne peut pas depasser 250 caracteres.");

            RuleFor(project => project.Description)
                .MaximumLength(1000).WithMessage("La description ne peut pas depasser 1000 caracteres.");

            // Validation rule for MinPrice
            /*RuleFor(project => project.MinPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Minimum price must be greater than or equal to 0.");

            // Validation rule for MaxPrice
            RuleFor(project => project.MaxPrice)
                .GreaterThanOrEqualTo(project => project.MinPrice).WithMessage("Maximum price must be greater than or equal to the minimum price.");

            // Validation rule for Images
            RuleFor(project => project.Images)
                .NotEmpty().WithMessage("Project images are required.");

            // Validation rule for Latitude
            RuleFor(project => project.Latitude)
                .NotEmpty().WithMessage("Latitude is required.");*/

            RuleFor(project => project.ProjectId)
                .NotEmpty().WithMessage("Project Id must not be empty.");

            // Validation rule for NumberOfUnits
            RuleFor(project => project.NumberOfUnits)
                .GreaterThanOrEqualTo(0).WithMessage("Number of units must be greater than or equal to 0.");

            // Validation rules for Min and Max Sellable Surface Range. Neither
            // is collected by the creation UI (ProjectsListPage/ImmeublesListPage
            // only ask for name/location/type/price/description), so both wire
            // in as 0 by default — the same "not actually required, just
            // GreaterThanOrEqualTo" convention UpdateImmeubleValidator already
            // uses is applied here instead of the previous GreaterThan(0)/
            // GreaterThan(Min) rules, which rejected every real submission
            // with an unactionable "One or more validation errors occurred."
            // (F2).
            RuleFor(project => project.MinSellableSurfaceRange)
                .GreaterThanOrEqualTo(0).WithMessage("Minimum sellable surface range must be greater than or equal to 0.");

            RuleFor(project => project.MaxSellableSurfaceRange)
                .GreaterThanOrEqualTo(project => project.MinSellableSurfaceRange).WithMessage("Maximum sellable surface range must be greater than or equal to the minimum sellable surface range.");
        }
    }
}
