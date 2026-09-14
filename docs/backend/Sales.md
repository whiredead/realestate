# Sales

`src/ProjectAPI/src/Api/Controllers/SalesController.cs` — class `SalesController`, route prefix **`api/sales`**.

## Purpose

Read-only reporting over the `Sale` table: a company-wide list for the admin
console, and a per-user list for a buyer.

`Sale` rows are written by `UpdateNotaryAppointmentHandler` when a notary
appointment's outcome is `PURCHASE_COMPLETED` (see
`docs/backend/NotaryAppointments.md`). This controller has no write endpoint —
the file carries a comment stating that a create-sale action and a
`{saleId}/payments` action were removed, and no handler in the solution is
dispatched to replace them.

## Endpoints

| Verb | Path | Roles | Handler | Response |
|---|---|---|---|---|
| GET | `/api/sales` | `AdminsAgents` | `GetAllSalesHandler` | `AllSalesResponse` |
| GET | `/api/sales/user/{userId}` | any authenticated | `GetSalesByUserHandler` | `UserSalesResponse` |

Class-level `[Authorize]`; the company-wide list adds
`RoleGroups.AdminsAgents`.

## `GET /api/sales`

`GetAllSalesHandler` builds the row set by joining
`Sale → Unit → Immeuble → Project`, filtered by
`ProjectScopeService.GetScopedProjectIdsAsync` (a `null` result means
unrestricted, i.e. `GLOBAL_ADMIN`). Optional filters: `ProjectId`, `From`, `To`,
and a `Search` term matched against buyer first/last name, unit number, immeuble
name and project name. Rows are ordered by `SaleDate` descending.

**Collections are summed from two separate tables:**

| Source | Keyed on | Filter applied |
|---|---|---|
| `PaymentTracking` | `SaleId` | none — all rows summed |
| `Payment` | `Reservation.UnitId`, joined `Payment → Reservation` | `Status == PaymentStatus.Validated` |

The handler's comment describes `PaymentTracking` as the retired ledger that hung
off the sale, and `Payment` as the current immutable ledger that hangs off the
reservation, and states both are summed so historic rows are not lost.

The selling agent is resolved from `Reservation.OwnerSalesAgentId` for the sale's
unit; where a unit carries more than one reservation the most recent is used.
Agent names are then read from `Users`.

## `GET /api/sales/user/{userId}`

`GetSalesByUserHandler` checks ownership before reading:

```csharp
var isInternal = _currentUser.Roles.Any(role => RoleCodes.Internal.Contains(role, StringComparer.Ordinal));
if (!isInternal && !string.Equals(_currentUser.UserId, r.UserId, StringComparison.Ordinal))
    throw BusinessRuleException.BuyerScopeDenied();
```

So an internal role may read any user's sales; a buyer-only caller may read only
their own. It then loads sales via `ISaleRepository.GetByBuyerAsync` and attaches
the matching `Purchase` rows keyed on `SaleId`.

The controller wraps this action in `try/catch (Exception)` and, on failure,
returns **500 with an `ErrorResponse`** containing `ex.Message`,
`ex.GetType().Name` and `ex.InnerException?.Message`, plus a generated
`RequestId` that is also written to the log. This bypasses `ApiExceptionFilter`,
so a `BusinessRuleException` raised by the ownership check above surfaces as a
500 rather than the filter's 403. `ErrorResponse` is declared in the same file as
the controller.

## Orphaned handlers

`Api/Application/Sales/` contains `CreateSale` (command, handler, response,
validator) and `AddPayment` (command, handler). A repository-wide search for
`CreateSaleCommand` and `AddPaymentCommand` outside their own folders returns no
results — **no controller or handler dispatches either**. MediatR registers them
through assembly scanning, so they are resolvable but unreachable.

`CreateSaleHandler` is also referenced indirectly: `UpdateNotaryAppointmentHandler`
constructs a `Sale` entity inline rather than sending `CreateSaleCommand`.

`GetSalesMonthlySummary` exists in the same folder; it is not dispatched by this
controller. Its consumer is **Unverified** — see `AdminDashboard` (pending).

## Dependencies

**Services:** `ISaleRepository`, `IPurchaseRepository`, `ApplicationDbContext`,
`ProjectScopeService`, `ICurrentUser`, `ILogger<SalesController>`.

**Tables read:** `Sales`, `Units`, `Immeubles`, `Projects`, `PaymentTracking`,
`Payments`, `Reservations`, `Purchases`, `Users`, `ProjectMemberships`.

**Tables written:** none.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| NotaryAppointments | Sole writer of the `Sale` rows this module reads. |
| Payments | Sums `Payment` rows joined through `Reservation`, filtered to `Validated`. |
| Reservations | Supplies `OwnerSalesAgentId` and the `Payment → Reservation → Unit` path. |
| Immeuble / Units / Projects | Join path and perimeter resolution. |
| Purchases | Attached per sale in the per-user response. |
| ProjectMembership | `ProjectScopeService` on the company-wide list. |

**Depended on by**

| Module | Mechanism |
|---|---|
| Deliveries | `ScheduleDeliveryHandler` requires an existing `Sale` by id. |
| Projects | `GetProjectByIdHandler` counts `Sale` rows in the trailing 90 days for per-building velocity; `RemoveProjectHandler` deletes them. |
| Immeuble | `DeleteImmeublesHandler` deletes sales for the building's units. |

## Known edge cases

- `PaymentTracking` rows are summed with no status filter while `Payment` rows
  are filtered to `Validated`. The two ledgers are added together, so a unit
  carrying rows in both contributes from both.
- The `Payment` ledger is folded onto a sale **by `UnitId`**, not by reservation.
  Where a unit has more than one reservation with payments, all of them are
  attributed to the single sale for that unit.
- `GET /api/sales/user/{userId}` converts every exception, including the
  authorization denial it raises itself, into a 500 carrying the exception
  message and type name.
- `GetSalesByUserHandler` permits any internal role — including `TECHNICIAN` —
  to read any user's sales; there is no project-perimeter check on this action.
- `Sale` carries no `ReservationId`, so a sale cannot be correlated back to the
  reservation that produced it except through `UnitId`.
- No write endpoint exists, but `CreateSaleHandler` and `AddPaymentHandler`
  remain in the assembly and are registered.

## Frontend coverage

`realestateFront/src/api/http/salesApi.ts`; UI in `features/sales/SalesPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| GET `/api/sales` | `salesApi.listAll` |
| GET `/api/sales/user/{userId}` | `salesApi.getByUser` |

Both endpoints are called. `salesApi.listAll` passes the project, date-range and
search filters through `buildQuery`.
