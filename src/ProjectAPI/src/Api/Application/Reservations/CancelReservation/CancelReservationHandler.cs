using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.CancelReservation;

public class CancelReservationHandler : IRequestHandler<CancelReservationCommand, CancelReservationResponse>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IUnitStatusService _unitStatus;
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public CancelReservationHandler(
        IReservationRepository reservationRepository,
        IUnitStatusService unitStatus,
        ApplicationDbContext db,
        ProjectScopeService projectScope)
    {
        _reservationRepository = reservationRepository;
        _unitStatus = unitStatus;
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<CancelReservationResponse> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        // §6.4 — cancellation is admin-only and project-scoped.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, cancellationToken);

        // Validate that the reservation exists
        var reservation = await _reservationRepository.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation with ID {request.ReservationId} not found.");

        // §12.4 — only an APPROVED reservation can be cancelled. Previously this
        // handler applied the change unconditionally, so an already Sold, Rejected
        // or Expired reservation could be "cancelled".
        ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Cancelled);

        // §3 — returning a RESERVED unit to AVAILABLE is never automatic; it is
        // exactly this administrative cancellation. The release is recorded with
        // its cause so the history explains why the unit went back on sale.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        reservation.Status = ReservationStatus.Cancelled;

        // A draft / pending sale on a cancelled file used to stay ACTIVE: it kept
        // the unit "sold in progress" (IX_Sales_ActivePerUnit) and blocked the next
        // buyer's sale. A confirmed sale cannot exist here (it would have converted
        // the reservation).
        var openSales = await _db.Set<ProjectAPI.Domain.Sales.Entities.Sale>()
            .Where(s => s.ReservationId == reservation.Id
                     && (s.Status == ProjectAPI.Domain.Sales.Entities.SaleStatus.Draft
                         || s.Status == ProjectAPI.Domain.Sales.Entities.SaleStatus.PendingNotary))
            .ToListAsync(cancellationToken);
        foreach (var sale in openSales)
        {
            sale.Status = ProjectAPI.Domain.Sales.Entities.SaleStatus.Cancelled;
            sale.Notes = string.IsNullOrWhiteSpace(sale.Notes)
                ? "[Annulée avec la réservation]"
                : $"{sale.Notes}\n[Annulée avec la réservation]";
        }

        await _unitStatus.TransitionAsync(
            reservation.UnitId,
            UnitCommercialStatus.Available,
            UnitStatusCause.ReservationCancelled,
            reservationId: reservation.Id,
            ct: cancellationToken);

        // Save changes
        await _reservationRepository.SaveAsync();

        await transaction.CommitAsync(cancellationToken);

        return new CancelReservationResponse
        {
            Success = true,
            Message = "Reservation cancelled successfully."
        };
    }
}
