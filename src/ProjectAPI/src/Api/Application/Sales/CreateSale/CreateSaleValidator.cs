using ProjectAPI.Domain.Sales.CreateSale;

namespace ProjectAPI.Api.Application.Sales.CreateSale;

public class CreateSaleValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.TotalPrice).GreaterThan(0);
        RuleFor(x => x.UnitId).NotEmpty();

        // Must have BuyerId OR required manual fields (CIN is optional)
        RuleFor(x => x).Must(x =>
            !string.IsNullOrWhiteSpace(x.BuyerId) ||
            (!string.IsNullOrWhiteSpace(x.BuyerFirstName) &&
             !string.IsNullOrWhiteSpace(x.BuyerLastName) &&
             !string.IsNullOrWhiteSpace(x.BuyerEmail) &&
             !string.IsNullOrWhiteSpace(x.BuyerPhoneNumber)))
        .WithMessage("Provide BuyerId or full buyer info (FirstName, LastName, Email, PhoneNumber). CIN is optional.");
    }
}
