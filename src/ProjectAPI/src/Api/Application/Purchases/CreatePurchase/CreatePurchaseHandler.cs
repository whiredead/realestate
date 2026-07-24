using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Purchases.Interfaces;

namespace ProjectAPI.Api.Application.Purchases.CreatePurchase
{
    /// <summary>
    /// Handler for creating a new purchase.
    /// </summary>
    public class CreatePurchaseHandler : IRequestHandler<CreatePurchaseCommand, CreatePurchaseResponse>
    {
        private readonly IPurchaseRepository _purchaseRepository;

        public CreatePurchaseHandler(IPurchaseRepository purchaseRepository)
        {
            _purchaseRepository = purchaseRepository;
        }

        public async Task<CreatePurchaseResponse> Handle(CreatePurchaseCommand request, CancellationToken cancellationToken)
        {
            var purchase = new Purchase
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                SaleId = request.SaleId,
                ReservationId = request.ReservationId,
                NotaryAppointmentId = request.NotaryAppointmentId,
                TotalPrice = request.TotalPrice,
                PaidAmount = request.PaidAmount,
                RemainingAmount = request.RemainingAmount,
                CreatedAt = DateTime.Now
            };

            await _purchaseRepository.InsertAsync(purchase);
            await _purchaseRepository.SaveAsync();

            return new CreatePurchaseResponse
            {
                Id = purchase.Id,
                Message = "Purchase created successfully."
            };
        }
    }
}
