# Fixes — Unit context (backend)

## Items about a unit carried no information about the unit
- **Files:** new `Application/Common/Units/UnitLocation.cs` (`IHasUnitLocation`, `UnitContextDto` = project / building / floor / unit details, `UnitLocations.ForUnitsAsync`, `ForReservationsAsync`, `WithLocation`); new `Application/Units/GetUnitContext/*` + `GET api/Immeuble/units/{unitId}/context` (internal staff); reservation list / by-id / mine, sale responses, notary list / by-id, claims list / detail / eligible units, final-visit case
- **Wrong:** claims, reservations, notary appointments and final visits exposed a unit id (at best a frozen label); nothing returned the project, building, floor and unit details (location, address, phase, progress, warranty; building type; floor order; unit number, status, rooms, surfaces, view, orientation, price) the screens need.
- **Changed:** each of those responses carries `unitContext` (loaded in one batched query per page, never per row).

## Claims list status filter applied after paging (earlier in this session)
- **Files:** `Application/Sales/AfterSales/GetClaims/GetClaimsHandler.cs`
- **Changed:** `Status` / `UnitId` filters run in SQL before paging; `AssignedAgentId` returned.
