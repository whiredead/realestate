namespace ProjectAPI.Api.Application.Units.Pricing;

/// <summary>
/// Keeps the commercial calculations used in the pricing workbook in one
/// place. Inputs remain editable; derived values are always recomputed so the
/// unit record cannot contain a stale total or sellable surface.
/// </summary>
public static class UnitPricingCalculator
{
    public static UnitPricingResult Calculate(UnitPricingInput input)
    {
        var apartment = PositiveOrZero(input.ApartmentSurface);
        var balcony = PositiveOrZero(input.BalconySurface);
        var terrace = PositiveOrZero(input.TerraceSurface);
        var garden = PositiveOrZero(input.GardenSurface);

        // Pricing Zenata workbook formulas:
        // SUP TOTAL = app + balcony + terrace + garden
        // SV        = app + 50% balcony + 50% terrace + 30% garden
        // SV1       = app + 100% balcony + 50% terrace + 30% garden
        var totalSurface = apartment + balcony + terrace + garden;
        var saleableValue = apartment + (balcony * .5d) + (terrace * .5d) + (garden * .3d);
        var saleableValue1 = apartment + balcony + (terrace * .5d) + (garden * .3d);

        var latestPrice = input.LatestPrice;
        var priceSaleableValue = input.PriceSaleableValue;
        var priceSaleableValue1 = input.PriceSaleableValue1;

        // A user may choose the price they know. The remaining two commercial
        // values are derived from it, following the same relationship as the
        // workbook. LatestPrice has priority when it was explicitly supplied.
        if (latestPrice.HasValue)
        {
            priceSaleableValue = Divide(latestPrice.Value, saleableValue);
            priceSaleableValue1 = Divide(latestPrice.Value, saleableValue1);
        }
        else if (priceSaleableValue.HasValue)
        {
            latestPrice = RoundMoney(priceSaleableValue.Value * (decimal)saleableValue);
            priceSaleableValue1 = latestPrice.HasValue ? Divide(latestPrice.Value, saleableValue1) : null;
        }
        else if (priceSaleableValue1.HasValue)
        {
            latestPrice = RoundMoney(priceSaleableValue1.Value * (decimal)saleableValue1);
            priceSaleableValue = latestPrice.HasValue ? Divide(latestPrice.Value, saleableValue) : null;
        }

        return new UnitPricingResult(totalSurface, saleableValue, saleableValue1, priceSaleableValue, priceSaleableValue1, latestPrice);
    }

    private static double PositiveOrZero(double? value) => Math.Max(0, value ?? 0);

    private static decimal? Divide(decimal amount, double divisor) =>
        divisor <= 0 ? null : RoundMoney(amount / (decimal)divisor);

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record UnitPricingInput(
    double? ApartmentSurface,
    double? BalconySurface,
    double? TerraceSurface,
    double? GardenSurface,
    decimal? PriceSaleableValue,
    decimal? PriceSaleableValue1,
    decimal? LatestPrice);

public sealed record UnitPricingResult(
    double TotalSurface,
    double SaleableValue,
    double SaleableValue1,
    decimal? PriceSaleableValue,
    decimal? PriceSaleableValue1,
    decimal? LatestPrice);
