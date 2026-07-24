namespace ProjectAPI.Api.Application.Immeubles.UpdateImmeuble
{
    /// <summary>
    /// Validator for the <see cref="UpdateImmeubleCommand"/> class.
    /// </summary>
    public class UpdateImmeubleValidator : AbstractValidator<UpdateImmeubleCommand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateImmeubleValidator"/> class.
        /// </summary>
        public UpdateImmeubleValidator()
        {
            // Validate Name
            RuleFor(command => command.Name)
                .NotEmpty().WithMessage("Project name is required.")
                .MaximumLength(150).WithMessage("Project name must not exceed 150 characters.")
                .When(command => !string.IsNullOrWhiteSpace(command.Name));

            // Validate Location
            RuleFor(command => command.Location)
                .NotEmpty().WithMessage("Project location is required.")
                .When(command => !string.IsNullOrWhiteSpace(command.Location));

            // Validate Type
            RuleFor(command => command.Type)
                .NotEmpty().WithMessage("Property type is required.")
                .Must(BeAValidType).WithMessage("Invalid property type.")
                .When(command => !string.IsNullOrWhiteSpace(command.Type));

            // Validate MinPrice
            RuleFor(command => command.MinPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Minimum price must be greater than or equal to 0.")
                .When(command => command.MinPrice >= 0);

            // Validate MaxPrice
            RuleFor(command => command.MaxPrice)
                .GreaterThanOrEqualTo(command => command.MinPrice).WithMessage("Maximum price must be greater than or equal to the minimum price.")
                .When(command => command.MaxPrice >= 0 && command.MinPrice >= 0);

            // Validate MinSellableSurfaceRange
            RuleFor(command => command.MinSellableSurfaceRange)
                .GreaterThanOrEqualTo(0).WithMessage("Minimum sellable surface range must be greater than or equal to 0.")
                .When(command => command.MinSellableSurfaceRange >= 0);

            // Validate MaxSellableSurfaceRange
            RuleFor(command => command.MaxSellableSurfaceRange)
                .GreaterThanOrEqualTo(command => command.MinSellableSurfaceRange).WithMessage("Maximum sellable surface range must be greater than or equal to the minimum sellable surface range.")
                .When(command => command.MaxSellableSurfaceRange >= 0 && command.MinSellableSurfaceRange >= 0);
        }

        /// <summary>
        /// Checks if the provided type is valid.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <returns>True if valid; otherwise, false.</returns>
        private bool BeAValidType(string type)
        {
            // Replace with your enum validation logic
            // Example: Enum.TryParse(typeof(PropertyTypeEnum), type, true, out _);
            return true; // Example always returns true for simplicity
        }
    }
}
