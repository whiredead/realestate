# Fixes — Pagination (ProjectAPI)

## GET /api/Projects paged twice and filtered the perimeter after paging
- **Files:** `src/ProjectAPI/src/Domain/Projects/Interfaces/IProjectRepository.cs`, `src/ProjectAPI/src/Infrastructure/Repositories/ProjectRepository.cs`, `src/ProjectAPI/src/Api/Application/Projects/GetAllProjects/GetAllProjectsHandler.cs`
- **Wrong:** the repository applied `Skip/Take`; the handler then removed out-of-scope projects and applied `Skip/Take` again. Page 2 was always empty, `totalItems` was the size of the filtered page (an agent asking 5 rows got "4 in total"), and projects beyond the first global page were invisible to any scoped user. No ordering, so page contents were unstable.
- **Changed:** the repository takes the scope set, filters before paging, returns `(Items, TotalCount)` counted on the same filter, ordered by name then id. The handler no longer re-pages.

## Unstable page contents (no ORDER BY before Skip/Take)
- **Files:** `Appointments/GetAppointments`, `Feedbacks/GetFeedback`, `Immeubles/GetAllImmeubles`, `Notary/Appointments/GetNotaryAppointments`, `ProjectAssignments/GetAllProjectAssignments`, `ProjectMemberships/GetAllProjectMemberships`, `Projects/LikedProjects/GetLikedProjects`, `Purchases/GetUserPurchases`, `Quartiers/GetQuartiers`, `Reservations/GetReservations`, `Units/GetAllUnits`, `Units/GetUnitsByProjectId` (all `*Handler.cs` under `src/ProjectAPI/src/Api/Application`)
- **Wrong:** paging an unordered sequence: rows could repeat or disappear between pages.
- **Changed:** deterministic order before paging (dates descending or name/number ascending, then id).

## Units by building: wrong total
- **Files:** `Units/GetUnitsByProjectId/GetUnitsByProjectIdHandler.cs`
- **Wrong:** `totalItems` was the count of the returned page, so clients never saw more than one page.
- **Changed:** total counted before paging.

## Paging parameters never validated
- **Files:** `Common/Behaviours/PaginationBehaviour.cs` (new), `DependencyInjection.cs`
- **Wrong:** `PageNumber=0` / negative `PageSize` reached `Skip(-n)` → 500; unbounded `PageSize` could load a whole table.
- **Changed:** MediatR behaviour for every query with `PageNumber`/`PageSize`: values < 1 → 422 with field errors; `PageSize` capped at 500 (echoed in the response).
