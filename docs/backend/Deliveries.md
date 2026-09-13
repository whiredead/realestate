# Deliveries

`src/ProjectAPI/src/Api/Controllers/DeliveriesController.cs` — class `DeliveriesController`, route prefix **`api/deliveries`**.

## Purpose

Records a `PropertyDelivery` row against an existing `Sale`, with a free-text
status and report note.

This is **not** the path that delivers a property. `PropertyDelivery` is a
separate table from the handover entities, and neither handler in this
controller touches `Unit.Status`, `Warranty`, or `CrmContact.LifecycleStatus`.
Moving a unit to `DELIVERED` and starting its warranty happens only in
`AcknowledgeHandoverHandler` — see `docs/backend/Handovers.md`.

## Endpoints

| Verb | Path | Handler | Request | Response |
|---|---|---|---|---|
| POST | `/api/deliveries` | `ScheduleDeliveryHandler` | `ScheduleDeliveryCommand` | `Guid` (raw) |
| PATCH | `/api/deliveries/{deliveryId:guid}/status` | `UpdateDeliveryStatusHandler` | `UpdateDeliveryStatusCommand` | `bool` (raw) |

Both actions return the mediator result directly (`Task<Guid>` / `Task<bool>`)
rather than an `IActionResult`, so the response body is a bare JSON scalar.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.AdminsAgents)]` —
`GLOBAL_ADMIN`, `PROJECT_ADMIN`, `SALES_AGENT`. There are no per-action
attributes.

Both handlers call `ProjectScopeService.EnsureUnitProjectAccessAsync`, which
resolves the unit's project through `unit.ProjectId → Immeuble.ProjectId` and
checks the caller's `ProjectMembership`. `UpdateDeliveryStatusHandler` scopes on
the stored `delivery.UnitId` rather than a caller-supplied one.

## Behaviour

`ScheduleDeliveryHandler`:

1. Loads the `Sale` by `SaleId`; `NotFoundException` if absent. The loaded value
   is discarded (`_ = await …`) — it exists only as an existence check.
2. `EnsureUnitProjectAccessAsync(r.UnitId)`.
3. Inserts a `PropertyDelivery` with `SaleId`, `UnitId`, `DeliveryDate`,
   `Status` and `Report` taken verbatim from the request.
4. Returns the new id.

`Status` defaults to the string `"Scheduled"` and `Report` to
`"Delivery scheduled"` on the command. Neither is validated against a list of
permitted values.

`UpdateDeliveryStatusHandler` loads the delivery (404 if absent), scopes on its
unit, overwrites `Status` and `Report` with the request values, and returns
`true`. There is no state machine and no check on the previous value.

## Relationship to Handovers

Both mechanisms exist in the codebase and operate on different tables:

| | Deliveries | Handovers |
|---|---|---|
| Table | `PropertyDeliveries` | `HandoverAppointments`, `HandoverReports`, `HandoverItems` |
| Requires | an existing `Sale` row | `reservation.Status == Sold` |
| Status type | free-text `string` | `AppointmentAttemptStatus` / `HandoverReportStatus` enums |
| Writes `Unit.Status` | no | yes — `DELIVERED` |
| Creates `Warranty` | no | yes |
| Duplicate guard | none | `HANDOVER_ALREADY_ACTIVE` |

`UpdateNotaryAppointmentHandler` creates the `Sale` row that
`ScheduleDeliveryHandler` requires, so this controller is reachable only after a
notarial conversion. Nothing in either module reads the other's rows.

## Dependencies

**Services:** `IPropertyDeliveryRepository`, `ISaleRepository`,
`ProjectScopeService`.

**Tables written:** `PropertyDeliveries`.
**Tables read:** `Sales`, `Units`, `Immeubles`, `ProjectMemberships`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| Sales | `ScheduleDeliveryHandler` requires an existing `Sale`. See `docs/backend/Sales.md`. |
| Immeuble / Units | Perimeter resolution via `EnsureUnitProjectAccessAsync`. |
| ProjectMembership | `ProjectScopeService`. |

**Depended on by** — none traced. No other module reads `PropertyDeliveries`.

## Known edge cases

- No read endpoint exists. `PropertyDelivery` rows can be created and updated but
  never listed or fetched through this API.
- `Status` is an unvalidated free-text string on both endpoints; any value is
  accepted and overwrites the previous one.
- No duplicate guard: scheduling twice for the same `SaleId`/`UnitId` creates two
  `PropertyDelivery` rows.
- `ScheduleDeliveryCommand.SaleId` and `UnitId` are independent request fields;
  nothing checks that the sale refers to that unit.
- Neither handler writes an audit or history row.
- Neither command has an `AbstractValidator`.

## Frontend coverage

`realestateFront/src/api/http/deliveriesApi.ts`; UI in
`features/deliveries/DeliveriesPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| POST `/api/deliveries` | `deliveriesApi.schedule` |
| PATCH `/api/deliveries/{id}/status` | `deliveriesApi.updateStatus` |

`deliveriesApi.list()` returns a hard-coded `[]` and makes no request; its
comment states no list endpoint exists, which matches the controller.

`deliveriesApi.schedule` sends `status: undefined` (overriding any caller value)
so the backend default `"Scheduled"` applies, then constructs the returned object
locally, mapping `"Scheduled"` through `handoverStatusString.fromLegacy`. The
adapter types the response as `string`, which matches the bare `Guid` the action
returns.

The adapter's return type is `HandoverAppointment`, so the frontend presents
`PropertyDelivery` rows in the same shape as handover appointments even though
the two are unrelated tables server-side.
