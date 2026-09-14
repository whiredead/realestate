# Fixes — Dashboard (backend)

## Dashboard lacked construction progress and stock by state; no project filter
- **Files:** `Application/AdminDashboards/Dashboard/AdminDashboardQuery.cs`, `AdminDashboardResponse.cs`, `GetAdminDashboardHandler.cs`
- **Wrong:** the overview had reservations (count, amount, rate, average) but no construction progress and no available / reserved / sold / delivered unit counts; and every figure could only be seen for the whole perimeter.
- **Changed:** `ProjectId` query filter (narrows the perimeter, never widens it). New figures: `ConstructionProgressPct` (weighted milestones, averaged over projects in scope; a project without milestones counts with its stored progress), `TotalUnits`, `AvailableUnits`, `ReservedUnits` (hold / reserved / contracted), `SoldUnits`, `DeliveredUnits`.
