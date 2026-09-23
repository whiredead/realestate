namespace ProjectAPI.Api.Application.Immeubles.CreateImmeuble;

/// <summary>
/// Command to create a new immeuble.
/// </summary>
public class CreateImmeubleCommand : IRequest<Guid>
{
    /// <summary>
    /// Gets or sets the name of the immeuble.
    /// </summary>
    public string Name { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the location of the immeuble.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the images associated with the immeuble.
    /// </summary>
    public string Images { get; set; }

    /// <summary>
    /// Gets or sets the description of the immeuble.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Unused: CreateImmeubleHandler always assigns "ComingSoon" regardless of
    /// this value (a new building's status is not a caller choice). Nullable
    /// because neither creation form sends it — an omitted non-nullable
    /// string here made [ApiController]'s automatic model binding reject the
    /// request with "The Status field is required" before FluentValidation
    /// or the handler ever ran (F2's actual root cause: the relaxed surface-
    /// range validator was correct but never reached).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the number of units in the immeuble.
    /// </summary>
    public int NumberOfUnits { get; set; }

    /// <summary>
    /// Gets or sets the minimum sellable surface area range of the immeuble.
    /// </summary>
    public int MinSellableSurfaceRange { get; set; }

    /// <summary>
    /// Gets or sets the maximum sellable surface area range of the immeuble.
    /// </summary>
    public int MaxSellableSurfaceRange { get; set; }
    public string? Module3DLink { get; set; }
    public int NumberOfSoldUnites { get; set; }

    /// <summary>
    /// Gets or sets the total number of units still available in the Immeuble.
    /// </summary>
    public int NumberOfAvailableUnites { get; set; }

    /// <summary>
    /// Gets or sets the percentage of units sold in the Immeuble.
    /// </summary>
    public int SellsPercentage { get; set; }
}
