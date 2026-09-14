# Fixes — Performance (backend)

## Reservation list loaded every reservation into memory on each page request
- **Files:** `Application/Reservations/GetReservations/GetReservationsHandler.cs`
- **Wrong:** the handler called the repository's `Find` (materialises all matching rows), counted and paged in C#, once per page — with a few hundred reservations `GET /api/Reservations/list` took up to 28 s under load and dragged the whole API down.
- **Changed:** filter, count, order and `Skip/Take` run in SQL; documents are included for the page only.

## Scope lists sent to SQL as literal IN (...) lists — a new plan per call
- **Files:** `Application/Common/Security/ProjectScopeService.cs` (`GetScopedProjectIdsAsync` returns `List<Guid>`), `AdminDashboards/Dashboard/GetAdminDashboardHandler.cs`, `Sales/AfterSales/GetClaims/GetClaimsHandler.cs`, `Projects/GetProjectById/GetProjectByIdHandler.cs`, `Infrastructure/Repositories/ProjectRepository.cs`
- **Wrong:** EF Core inlines `HashSet<T>.Contains` into `IN ('…','…')` constants, so every perimeter/page produced a distinct statement to compile; on SQL Server Express this filled the plan cache and memory grants and stalled every request for 20-30 s at a time.
- **Changed:** the id collections used inside queries are `List<T>` (EF parameterises them through OPENJSON: one cached plan).

## Project list loaded staffing for every project (earlier in this session)
- **Files:** `Infrastructure/Repositories/ProjectRepository.cs`
- **Wrong:** members/agents were loaded for all projects, not the page.
- **Changed:** staffing is loaded for the page's project ids only.
