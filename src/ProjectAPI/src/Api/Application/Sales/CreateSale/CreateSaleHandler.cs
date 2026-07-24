namespace ProjectAPI.Api.Application.Sales.CreateSale;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Sales.CreateSale;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Domain.Users.Entities;

public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, CreateSaleResponse>
{
    private readonly ISaleRepository _saleRepo;
    private readonly IPaymentTrackingRepository _paymentRepo;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly IUnitRepository _unitRepo;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<CreateSaleHandler> _logger;

    public CreateSaleHandler(
        ISaleRepository saleRepo,
        IPaymentTrackingRepository paymentRepo,
        IPurchaseRepository purchaseRepo,
        IUnitRepository unitRepo,
        UserManager<User> userManager,
        ILogger<CreateSaleHandler> logger)
    {
        _saleRepo = saleRepo;
        _paymentRepo = paymentRepo;
        _purchaseRepo = purchaseRepo;
        _unitRepo = unitRepo;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<CreateSaleResponse> Handle(CreateSaleCommand r, CancellationToken ct)
    {
        _logger.LogInformation("[CreateSale] Starting sale creation. UnitId: {UnitId}, BuyerId: {BuyerId}, ReservationId: {ReservationId}",
            r.UnitId, r.BuyerId ?? "N/A", r.ReservationId?.ToString() ?? "N/A");

        try
        {
            // Step 1: Validate unit exists
            _logger.LogDebug("[CreateSale] Step 1: Validating unit exists...");
            var unit = await _unitRepo.GetByIDAsync(r.UnitId);
            if (unit == null)
            {
                _logger.LogWarning("[CreateSale] Unit not found: {UnitId}", r.UnitId);
                throw new NotFoundException($"Unit {r.UnitId} not found.");
            }
            _logger.LogDebug("[CreateSale] Unit found: {UnitNumber}", unit.UnitNumber);

            // Step 2: Fill buyer snapshot
            _logger.LogDebug("[CreateSale] Step 2: Filling buyer snapshot...");
            string? first, last, email, phone, cin;
            string buyerId = r.BuyerId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(r.BuyerId))
            {
                _logger.LogDebug("[CreateSale] Using BuyerId path: {BuyerId}", r.BuyerId);
                var user = await _userManager.FindByIdAsync(r.BuyerId!);
                if (user == null)
                {
                    _logger.LogWarning("[CreateSale] Buyer user not found: {BuyerId}", r.BuyerId);
                    throw new NotFoundException($"Buyer with id {r.BuyerId} not found.");
                }

                first = user.FirstName;
                last = user.LastName;
                email = user.Email ?? "";
                phone = user.PhoneNumber ?? "";
                cin = null; // CIN not stored on User entity - nullable now
                _logger.LogDebug("[CreateSale] Buyer info from user: {FirstName} {LastName}, Email: {Email}", first, last, email);
            }
            else
            {
                // Manual path
                _logger.LogDebug("[CreateSale] Using manual buyer info path");
                first = r.BuyerFirstName!;
                last = r.BuyerLastName!;
                email = r.BuyerEmail!;
                phone = r.BuyerPhoneNumber!;
                cin = r.BuyerCIN; // Can be null
                _logger.LogDebug("[CreateSale] Manual buyer info: {FirstName} {LastName}, Email: {Email}, CIN: {CIN}",
                    first, last, email, cin ?? "N/A");
            }

            // Step 3: Create Sale entity
            _logger.LogDebug("[CreateSale] Step 3: Creating Sale entity...");
            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                BuyerId = string.IsNullOrWhiteSpace(buyerId) ? null : buyerId,
                BuyerFirstName = first,
                BuyerLastName = last,
                BuyerEmail = email,
                BuyerPhoneNumber = phone,
                BuyerCIN = cin,
                UnitId = r.UnitId,
                SaleDate = r.SaleDate,
                TotalPrice = r.TotalPrice,
                IsUnderConstruction = r.IsUnderConstruction
            };
            _logger.LogDebug("[CreateSale] Sale entity created with ID: {SaleId}", sale.Id);

            await _saleRepo.InsertAsync(sale);
            _logger.LogDebug("[CreateSale] Sale inserted into repository");

            // Step 4: Optional initial payment
            if (r.InitialPaymentAmount.GetValueOrDefault() > 0)
            {
                _logger.LogDebug("[CreateSale] Step 4: Creating initial payment of {Amount}", r.InitialPaymentAmount);
                await _paymentRepo.InsertAsync(new PaymentTracking
                {
                    Id = Guid.NewGuid(),
                    SaleId = sale.Id,
                    AmountPaid = r.InitialPaymentAmount!.Value,
                    PaymentDate = DateTime.UtcNow,
                    Status = "Completed"
                });
                _logger.LogDebug("[CreateSale] Initial payment created");
            }
            else
            {
                _logger.LogDebug("[CreateSale] Step 4: No initial payment (amount: {Amount})", r.InitialPaymentAmount);
            }

            // Step 5: Purchase attach/create
            _logger.LogDebug("[CreateSale] Step 5: Handling Purchase entity...");
            Purchase? purchase = null;
            if (r.ReservationId.HasValue)
            {
                _logger.LogDebug("[CreateSale] Looking for existing purchase with ReservationId: {ReservationId}", r.ReservationId);
                var list = await _purchaseRepo.Find(p => p.ReservationId == r.ReservationId);
                purchase = list.FirstOrDefault();
                _logger.LogDebug("[CreateSale] Existing purchase found: {Found}", purchase != null);
            }

            if (purchase == null)
            {
                _logger.LogDebug("[CreateSale] Creating new Purchase entity");
                purchase = new Purchase
                {
                    Id = Guid.NewGuid(),
                    UserId = sale.BuyerId ?? "",
                    SaleId = sale.Id,
                    ReservationId = r.ReservationId,
                    TotalPrice = sale.TotalPrice,
                    PaidAmount = r.InitialPaymentAmount ?? 0,
                    RemainingAmount = sale.TotalPrice - (r.InitialPaymentAmount ?? 0),
                    CreatedAt = DateTime.UtcNow
                };
                await _purchaseRepo.InsertAsync(purchase);
                _logger.LogDebug("[CreateSale] New Purchase created with ID: {PurchaseId}", purchase.Id);
            }
            else
            {
                _logger.LogDebug("[CreateSale] Updating existing Purchase: {PurchaseId}", purchase.Id);
                purchase.SaleId = sale.Id;
                purchase.TotalPrice = sale.TotalPrice;
                purchase.RemainingAmount = sale.TotalPrice - purchase.PaidAmount;
                _purchaseRepo.Update(purchase);
                _logger.LogDebug("[CreateSale] Existing Purchase updated");
            }

            // Step 6: Save all changes
            _logger.LogDebug("[CreateSale] Step 6: Saving all changes...");
            await _saleRepo.SaveAsync();
            _logger.LogDebug("[CreateSale] Sale saved");
            await _paymentRepo.SaveAsync();
            _logger.LogDebug("[CreateSale] Payments saved");
            await _purchaseRepo.SaveAsync();
            _logger.LogDebug("[CreateSale] Purchase saved");

            _logger.LogInformation("[CreateSale] Sale created successfully. SaleId: {SaleId}, PurchaseId: {PurchaseId}",
                sale.Id, purchase.Id);

            return new CreateSaleResponse
            {
                SaleId = sale.Id,
                PurchaseId = purchase.Id,
                Message = "Sale created successfully."
            };
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "[CreateSale] Not found error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CreateSale] Unexpected error during sale creation. UnitId: {UnitId}, BuyerId: {BuyerId}, Error: {ErrorMessage}, StackTrace: {StackTrace}",
                r.UnitId, r.BuyerId ?? "N/A", ex.Message, ex.StackTrace);
            throw;
        }
    }
}
