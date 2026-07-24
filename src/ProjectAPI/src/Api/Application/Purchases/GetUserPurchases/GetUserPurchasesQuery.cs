namespace ProjectAPI.Api.Application.Purchases.GetUserPurchases;

/// <summary>
/// Query to retrieve all purchases for a given user along with aggregated totals.
/// </summary>
public class GetUserPurchasesQuery : IRequest<PurchaseSummaryResponse>
{
    /// <summary>
    /// Gets or sets the ID of the user whose purchases are to be retrieved.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the page number for pagination (default is 1).
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size for pagination (default is 10).
    /// </summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Represents a single purchase item in the response.
/// </summary>
public class PurchaseResponseItem
{
    public Guid Id { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? NotaryAppointmentId { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PurchaseType { get; set; }
    public string? ProjectName { get; set; }
    public List<string>? ProjectImages { get; set; }

    public string? UnitNumber { get; set; }
    public string? ImmeubleName { get; set; }
}

/// <summary>
/// Response model summarizing the user’s purchases and aggregated totals.
/// </summary>
public class PurchaseSummaryResponse
{
    /// <summary>
    /// Gets or sets the list of purchase items.
    /// </summary>
    public List<PurchaseResponseItem> Purchases { get; set; } = new List<PurchaseResponseItem>();

    /// <summary>
    /// Gets or sets the total amount spent on reservations.
    /// </summary>
    public decimal TotalReservationsSpent { get; set; }

    /// <summary>
    /// Gets or sets the total amount spent on sales.
    /// </summary>
    public decimal TotalSalesSpent { get; set; }

    /// <summary>
    /// Gets or sets the total amount spent on notary appointments.
    /// </summary>
    public decimal TotalNotaryAppointmentsSpent { get; set; }

    /// <summary>
    /// Gets or sets the overall number of purchases for the user.
    /// </summary>
    public int TotalCount { get; set; }
}
