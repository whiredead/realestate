using Microsoft.Extensions.Logging;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Application.Reservations.ApproveReservation;

public class ApproveReservationHandler : IRequestHandler<ApproveReservationCommand, bool>
{
    private readonly IReservationRepository _reservationRepo;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly ILogger<ApproveReservationHandler> _logger;

    public ApproveReservationHandler(
        IReservationRepository reservationRepo,
        IPurchaseRepository purchaseRepo,
        ILogger<ApproveReservationHandler> logger)
    {
        _reservationRepo = reservationRepo;
        _purchaseRepo = purchaseRepo;
        _logger = logger;
    }

    public async Task<bool> Handle(ApproveReservationCommand request, CancellationToken ct)
    {
        _logger.LogInformation("[ApproveReservation] Starting approval for ReservationId: {ReservationId}", request.ReservationId);
        _logger.LogInformation("[ApproveReservation] Documents count in request: {Count}", request.Documents?.Count ?? 0);

        try
        {
            // Use GetByIdWithDocumentsAsync to load Documents collection for adding new documents
            _logger.LogInformation("[ApproveReservation] Fetching reservation with documents...");
            var reservation = await _reservationRepo.GetByIdWithDocumentsAsync(request.ReservationId)
                ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

            _logger.LogInformation("[ApproveReservation] Reservation found. Status: {Status}, BuyerId: {BuyerId}, Existing docs count: {DocsCount}", 
                reservation.Status, reservation.BuyerId, reservation.Documents?.Count ?? 0);

            // §12.4 — single source of truth for the lifecycle. Replaces the
            // previous ad-hoc checks so every handler enforces the same matrix.
            ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Approved);

            if (string.IsNullOrWhiteSpace(reservation.BuyerId))
                throw new ValidationException("Reservation has no BuyerId—cannot create purchase.");

            // Mark as approved
            _logger.LogInformation("[ApproveReservation] Marking reservation as approved...");
            reservation.Status = ReservationStatus.Approved;
            reservation.ValidatedAt = DateTime.UtcNow;
            reservation.ValidatedBy = request.AdminUserId;
            reservation.AdminNote = request.AdminNote;

            // Attach any uploaded documents - using direct insert to avoid navigation property issues
            var documentsToAdd = new List<ReservationDocument>();
            if (request.Documents?.Any() == true)
            {
                _logger.LogInformation("[ApproveReservation] Preparing {Count} documents for insertion...", request.Documents.Count);
                foreach (var d in request.Documents)
                {
                    _logger.LogInformation("[ApproveReservation] Creating document: FileName={FileName}, Url={Url}, ContentType={ContentType}, SizeBytes={SizeBytes}, DocumentType={DocumentType}",
                        d.FileName, d.Url, d.ContentType, d.SizeBytes, d.DocumentType);
                    
                    var doc = new ReservationDocument
                    {
                        Id = Guid.NewGuid(),
                        ReservationId = reservation.Id,
                        FileName = d.FileName,
                        Url = d.Url,
                        ContentType = d.ContentType,
                        SizeBytes = d.SizeBytes,
                        DocumentType = d.DocumentType,
                        UploadedAt = DateTime.UtcNow,
                        UploadedBy = request.AdminUserId
                    };
                    documentsToAdd.Add(doc);
                    _logger.LogInformation("[ApproveReservation] Document prepared with Id={DocId}", doc.Id);
                }
            }

            // Idempotency: check if a Purchase already exists for this reservation
            _logger.LogInformation("[ApproveReservation] Checking for existing purchase...");
            var existing = await _purchaseRepo.Find(p => p.ReservationId == reservation.Id);
            if (!existing.Any())
            {
                _logger.LogInformation("[ApproveReservation] No existing purchase, creating new one...");
                var purchase = new Purchase
                {
                    Id = Guid.NewGuid(),
                    UserId = reservation.BuyerId!,
                    ReservationId = reservation.Id,
                    TotalPrice = reservation.TotalPropertyPrice,
                    PaidAmount = reservation.ReservationAmount, // deposit already paid
                    RemainingAmount = reservation.TotalPropertyPrice - reservation.ReservationAmount,
                    CreatedAt = DateTime.UtcNow
                };

                await _purchaseRepo.InsertAsync(purchase);
                _logger.LogInformation("[ApproveReservation] Purchase created with Id={PurchaseId}", purchase.Id);
            }
            else
            {
                _logger.LogInformation("[ApproveReservation] Purchase already exists, skipping creation");
            }

            // Persist reservation changes first
            _logger.LogInformation("[ApproveReservation] Updating reservation...");
            _reservationRepo.Update(reservation);
            
            _logger.LogInformation("[ApproveReservation] Saving reservation changes...");
            await _reservationRepo.SaveAsync();
            
            // Now add documents directly via repository (after reservation is saved)
            if (documentsToAdd.Any())
            {
                _logger.LogInformation("[ApproveReservation] Adding {Count} documents to database...", documentsToAdd.Count);
                foreach (var doc in documentsToAdd)
                {
                    await _reservationRepo.AddDocumentAsync(doc);
                    _logger.LogInformation("[ApproveReservation] Document {DocId} added to context", doc.Id);
                }
                _logger.LogInformation("[ApproveReservation] Saving documents...");
                await _reservationRepo.SaveAsync();
                _logger.LogInformation("[ApproveReservation] Documents saved successfully");
            }
            
            _logger.LogInformation("[ApproveReservation] Saving purchase changes...");
            await _purchaseRepo.SaveAsync();

            _logger.LogInformation("[ApproveReservation] SUCCESS! Reservation {ReservationId} approved", request.ReservationId);
            return true;
        }
        catch (NotFoundException ex)
        {
            _logger.LogError(ex, "[ApproveReservation] NotFoundException: {Message}", ex.Message);
            throw;
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "[ApproveReservation] ValidationException: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ApproveReservation] UNEXPECTED ERROR: {Message}. StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            throw;
        }
    }
}
