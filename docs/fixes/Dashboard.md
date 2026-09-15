# Fixes — Dashboard (backend)

## Dashboard lacked construction progress and stock by state; no project filter
- **Files:** `Application/AdminDashboards/Dashboard/AdminDashboardQuery.cs`, `AdminDashboardResponse.cs`, `GetAdminDashboardHandler.cs`
- **Wrong:** the overview had reservations (count, amount, rate, average) but no construction progress and no available / reserved / sold / delivered unit counts; and every figure could only be seen for the whole perimeter.
- **Changed:** `ProjectId` query filter (narrows the perimeter, never widens it). New figures: `ConstructionProgressPct` (weighted milestones, averaged over projects in scope; a project without milestones counts with its stored progress), `TotalUnits`, `AvailableUnits`, `ReservedUnits` (hold / reserved / contracted), `SoldUnits`, `DeliveredUnits`.

## 2026-09-15 — PerformanceIndicator: lost leads, vanishing history, off-by-one
Found while seeding demo data for the agent-performance widgets, then fixed.

- **Leads silently lost:** `CreateNotaryAppointmentHandler` called `performanceIndicator?.IncrementLeadsGenerated()` — a no-op when the agent had no row yet (their first-ever conversion arriving via a notary appointment with no prior commercial appointment on record). The lead was gone for good; nothing ever created the row afterward. Now creates the row (LeadsGenerated=1) the same way `CreateAppointmentHandler` already does for appointments.
- **History vanishes every month:** `PerformanceIndicator` is one running-total row per agent, created once and incremented forever after; `RecordedAt` is set at creation and never touched again. `GetAdminDashboardHandler` filtered this table by `RecordedAt within the selected period`, so once the calendar rolled past the month the row was first created in, the agent's entire Leads/Appointments total disappeared from every future dashboard view — not "no activity", the row simply stopped matching. There is no per-period data in this table to filter by, so the filter is removed: these two figures are lifetime totals, like every other read of this table already treats them.
- **Off-by-one:** `CreateAppointmentHandler` created a new row at `AppointmentsScheduled = 0` on an agent's first-ever appointment (the call that IS the first appointment), then only incremented on subsequent ones — under-counting every agent by exactly 1, permanently. Now starts at 1.
