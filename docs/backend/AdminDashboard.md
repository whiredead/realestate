# Admin Dashboard

`src/ProjectAPI/src/Api/Controllers/AdminDashboardController.cs` — class `AdminDashboardController`, route prefix **`api/AdminDashboard`**.

This file uses the namespace-block style, so its attributes are indented rather
than at column zero.

## Purpose

A single aggregate read backing the admin console's landing page: headline
counts, period sales with a previous-period comparison, agent performance,
per-project velocity and sell-through, near-sellout buildings, and inventory by
project.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| GET | `/api/AdminDashboard/overview` | `GetAdminDashboardHandler` | `AdminDashboardResponse` |

`AdminDashboardQuery` is bound from the query string and accepts `Year`, `Month`,
`StartDate` and `EndDate`.

## Authorization

`[Authorize(Roles = RoleGroups.AdminsAgents)]` — `GLOBAL_ADMIN`,
`PROJECT_ADMIN`, `SALES_AGENT`. The controller carries an inline comment
recording that this was previously `Admins`-only, which left a `SALES_AGENT` able
to reach the page but receiving a 403 on its one call.

## Scoping

`GetAdminDashboardHandler` resolves the perimeter once and applies it to every
figure:

```csharp
var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(ct);   // null = unrestricted
var scopedUnitIds    = scopedProjectIds is null ? null : <units whose immeuble is in scope>;
var scopedAgentIds   = scopedProjectIds is null ? null : <UserIds of active ProjectMemberships in scope>;
```

Each figure then intersects with the relevant set:

| Figure | Scoped by |
|---|---|
| `TotalAgents` | `u is Agent` **and** `scopedAgentIds` |
| `TotalProjects` | `scopedProjectIds` |
| sales for the period and the previous period | `scopedUnitIds` |
| agent performance | `scopedAgentIds` against `PerformanceIndicator.AgentId` |

`scopedAgentIds` is built from active `ProjectMembership` rows without filtering
on `RoleCode`, so it includes any user with an active membership on an in-scope
project, of any role. `TotalAgents` narrows that with the `u is Agent` type check
(the TPH discriminator).

## Response shape

`AdminDashboardResponse` carries:

- `TotalAgents`, `TotalProjects`
- `SalesThisMonth`, `SalesVolumeThisMonth`
- `PeriodStart`, `PeriodEnd`, and `PreviousPeriod` as a `PeriodComparisonDto`
  with `SalesCount`, `SalesVolume`, `SalesCountChangePct`, `SalesVolumeChangePct`
- `AgentsPerformance` and `TopPerformers` as `AgentPerformanceDto` lists
- `SalesVelocityByProject` — `ProjectVelocityDto` with monthly points and
  `AverageUnitsPerMonth`
- `TopPerformingProjects` / `BottomPerformingProjects` — `ProjectSellThroughDto`
- `NearSelloutBuildings` — `ImmeubleNearSelloutDto`
- `InventoryByProject` — `ProjectInventoryDto`

## Dependencies

**Services:** `ApplicationDbContext`, `ProjectScopeService`.

**Tables written:** none.
**Tables read:** `Sales`, `Units`, `Immeubles`, `Projects`, `ProjectMemberships`,
`AspNetUsers`, `PerformanceIndicators`, `Reservations`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| Sales | Period sales figures read `Sale` rows, written only by `UpdateNotaryAppointmentHandler`. See `docs/backend/Sales.md`. |
| Immeuble / Units | Inventory, sell-through and near-sellout counts read `Unit.Status`. See `docs/backend/Immeuble.md`. |
| ProjectMembership | Supplies both the project perimeter and `scopedAgentIds`. |
| Projects | Project names and the per-project breakdowns. |
| Appointments / Projects (favourites) | `PerformanceIndicator` rows are written by `CreateAppointmentHandler`, `AddLikedProjectHandler`, `RemoveLikedProjectHandler` and `CreateNotaryAppointmentHandler`; this handler only reads them. |

**Depended on by** — nothing.

## Known edge cases

- `PerformanceIndicator` counters are the source for agent performance, and their
  writers have gaps documented elsewhere: `CreateAppointmentHandler` creates a new
  indicator initialised to `0` without incrementing, so an agent's first
  appointment is uncounted; `AddLikedProjectHandler` only increments for agents
  that already have an indicator row.
- `scopedAgentIds` ignores `RoleCode`, so a project's technicians and notaries are
  in the set before the `u is Agent` type filter narrows `TotalAgents`. Other
  agent-keyed figures use the unfiltered set.
- Sales figures derive from the `Sale` table, which is deduplicated by `UnitId` at
  creation, so a unit sold twice after a cancellation contributes one row.
- The response mixes period-scoped figures (`SalesThisMonth`) with
  all-time figures (`TotalProjects`, `TotalAgents`) in one object.

## Frontend coverage

`realestateFront/src/api/http/dashboardApi.ts`; UI in
`features/dashboard/DashboardPage.tsx` with the widgets `AgentPerformance`,
`CompareChip`, `InventorySnapshot`, `NearSelloutList`, `ProjectRankings` and
`SalesVelocity`.

| Endpoint | Frontend caller |
|---|---|
| GET `overview` | `dashboardApi.overview` |

The single endpoint is called. `dashboardApi.overview` sends `StartDate` and
`EndDate` only — the `Year` and `Month` query fields are never populated by this
frontend. It passes an inclusive end date computed locally.

All six dashboard widgets render from this one response; there is no second
dashboard call.
