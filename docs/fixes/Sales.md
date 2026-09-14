# Fixes — Sales (ProjectAPI)

## "Créer une vente" offered when the API would refuse it
- **Files:** `src/ProjectAPI/src/Api/Application/Sales/SaleDrafts/GetSaleEligibilityQuery.cs` (new), `Controllers/SalesController.cs`
- **Wrong:** the console could only guess from the reservation status; project phase, final visit and existing sale were checked only when the draft was posted.
- **Changed:** `GET /api/sales/eligibility/{reservationId}` (AdminsAgents, in scope) returns `{ canCreate, reasons[] }` with the same preconditions as `CreateSaleDraftHandler`.

## Sale did not say where it is
- **Files:** `SaleDrafts/SaleResponse.cs`, `GetSaleByReservationQuery.cs`, `CreateSaleDraftCommand.cs`, `UpdateSaleDraftCommand.cs`, `CancelSaleDraftCommand.cs`
- **Wrong:** the response had no project / building / floor / unit, required by the draft sale screen.
- **Changed:** `ProjectId/ProjectName`, `ImmeubleId/ImmeubleName`, `FloorName`, `UnitNumber` resolved by `SaleResponse.FromAsync` on every sale endpoint.

## PENDING_NOTARY was never reached
- **Files:** `Application/NotaryAppointments/UpdateNotaryAppointment/UpdateNotaryAppointmentHandler.cs`, `src/ProjectAPI/src/Domain/Sales/Entities/Sale.cs`
- **Wrong:** no code moved a sale to `PendingNotary`, so that state (and its editability) could not occur.
- **Changed:** confirming the dossier's notary appointment moves a Draft sale to PendingNotary; cancelled / rejected / no-show / superseded appointments, or a completed one with a non-purchase outcome, move it back to Draft (`PendingNotary → Draft` added to `SaleStateMachine`). Saved in the appointment's transaction.

## Cancelled reservation left its sale active
- **Files:** `Application/Reservations/CancelReservation/CancelReservationHandler.cs`
- **Wrong:** cancelling an approved reservation released the unit but kept its Draft/PendingNotary sale active, which kept `IX_Sales_ActivePerUnit` occupied and blocked the next buyer's sale on that unit.
- **Changed:** open sales of the reservation are cancelled in the same transaction (traced in the sale notes).
