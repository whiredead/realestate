using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Purchases.GetUserPurchases
{
    /// <summary>
    /// Handler to retrieve purchases for a given user and calculate aggregated totals.
    /// </summary>
    public class GetUserPurchasesHandler : IRequestHandler<GetUserPurchasesQuery, PurchaseSummaryResponse>
    {
        private readonly IPurchaseRepository _purchaseRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly ISaleRepository _saleRepository;

        public GetUserPurchasesHandler(IPurchaseRepository purchaseRepository, IReservationRepository reservationRepository,ISaleRepository saleRepository)
        {
            _purchaseRepository = purchaseRepository;
            _reservationRepository = reservationRepository;
            _saleRepository = saleRepository;
        }

        public async Task<PurchaseSummaryResponse> Handle(GetUserPurchasesQuery request, CancellationToken cancellationToken)
        {
            var purchases = await _purchaseRepository.Find(p => p.UserId == request.UserId);
            var totalCount = purchases.Count();

            var pagedPurchases = purchases
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var responseItems = new List<PurchaseResponseItem>();
            decimal totalReservationsSpent = 0;
            decimal totalSalesSpent = 0;
            decimal totalNotaryAppointmentsSpent = 0;

            foreach (var purchase in pagedPurchases)
            {
                string purchaseType;
                string? projectName = null;
                List<string>? projectImages = null;
                string? unitNumber = null;
                string? immeubleName = null;

                if (purchase.ReservationId.HasValue)
                {
                    purchaseType = "Reservation";
                    totalReservationsSpent += purchase.PaidAmount;

                    var reservations = await _reservationRepository.FindWithIncludesAsync(
                        r => r.Id == purchase.ReservationId.Value,
                        query => query
                            .Include(r => r.Unit)
                                .ThenInclude(u => u.Immeuble)
                                    .ThenInclude(i => i.Project));

                    var reservation = reservations.FirstOrDefault();
                    if (reservation?.Unit != null)
                    {
                        unitNumber = reservation.Unit.UnitNumber;
                        if (reservation.Unit.Immeuble != null)
                        {
                            immeubleName = reservation.Unit.Immeuble.Name;
                            if (reservation.Unit.Immeuble.Project != null)
                            {
                                projectName = reservation.Unit.Immeuble.Project.Name;
                                projectImages = reservation.Unit.Immeuble.Project.Images;
                            }
                        }
                    }
                }
                else if (purchase.SaleId.HasValue)
                {
                    purchaseType = "Sale";
                    totalSalesSpent += purchase.PaidAmount;

                    var sales = await _saleRepository.FindWithIncludesAsync(
                        s => s.Id == purchase.SaleId.Value,
                        query => query
                            .Include(s => s.Unit)
                                .ThenInclude(u => u.Immeuble)
                                    .ThenInclude(i => i.Project));

                    var sale = sales.FirstOrDefault();
                    if (sale?.Unit != null)
                    {
                        unitNumber = sale.Unit.UnitNumber;
                        if (sale.Unit.Immeuble != null)
                        {
                            immeubleName = sale.Unit.Immeuble.Name;
                            if (sale.Unit.Immeuble.Project != null)
                            {
                                projectName = sale.Unit.Immeuble.Project.Name;
                                projectImages = sale.Unit.Immeuble.Project.Images;
                            }
                        }
                    }
                }
                else if (purchase.NotaryAppointmentId.HasValue)
                {
                    purchaseType = "Notary Appointment";
                    totalNotaryAppointmentsSpent += purchase.PaidAmount;
                }
                else
                {
                    purchaseType = "Unknown";
                }

                responseItems.Add(new PurchaseResponseItem
                {
                    Id = purchase.Id,
                    SaleId = purchase.SaleId,
                    ReservationId = purchase.ReservationId,
                    NotaryAppointmentId = purchase.NotaryAppointmentId,
                    TotalPrice = purchase.TotalPrice,
                    PaidAmount = purchase.PaidAmount,
                    RemainingAmount = purchase.RemainingAmount,
                    CreatedAt = purchase.CreatedAt,
                    PurchaseType = purchaseType,
                    ProjectName = projectName,
                    ProjectImages = projectImages,
                    UnitNumber = unitNumber,
                    ImmeubleName = immeubleName
                });
            }

            return new PurchaseSummaryResponse
            {
                Purchases = responseItems,
                TotalReservationsSpent = totalReservationsSpent,
                TotalSalesSpent = totalSalesSpent,
                TotalNotaryAppointmentsSpent = totalNotaryAppointmentsSpent,
                TotalCount = totalCount
            };
        }
    }
}
