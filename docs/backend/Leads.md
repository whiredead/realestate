# Leads

`src/ProjectAPI/src/Api/Controllers/LeadsController.cs` — class `LeadsController`, route prefix **`api/Leads`**.

## Purpose

Read-only listing of `Lead` rows. This controller contains no write path; `Lead`
rows are created and deleted by the favourites handlers on
`ProjectsController` — `AddLikedProjectHandler` inserts one per building in the
liked project, `RemoveLikedProjectHandler` deletes them.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| GET | `/api/Leads` | `GetLeadsHandler` | `PaginatedResponse<LeadResponse>` |

`GetLeadsQuery` is bound from the query string and carries `ProjectId`,
`AgentId`, `PageNumber` and `PageSize`.

## Authorization

Two attributes stack on the class:

```csharp
[Authorize(AuthenticationSchemes = "Bearer")]
[Authorize(Roles = RoleGroups.AdminsAgents)]
```

`GLOBAL_ADMIN`, `PROJECT_ADMIN`, `SALES_AGENT`.

## Behaviour

`GetLeadsHandler`:

1. `ProjectScopeService.GetScopedProjectIdsAsync` — `null` means unrestricted
   (`GLOBAL_ADMIN`), otherwise the caller's own project ids.
2. A `SALES_AGENT` caller has `AgentId` forced to their own `ICurrentUser.UserId`,
   so a supplied `AgentId` is ignored for them. Other roles use the supplied
   value.
3. Filters: scoped project ids, then the optional `ProjectId` and the effective
   `AgentId`.
4. Counts, orders by `CreatedAt` descending, then applies `Skip`/`Take` — the
   pagination happens in the database query rather than in memory.
5. Resolves project names from `Projects` and user display names from `Users` for
   both `UserId` and `AgentId`, and projects them onto `LeadResponse`.

`LeadResponse` carries `Id`, `ProjectId`, `ProjectName`, `UserId`,
`UserFullName`, `AgentId`, `AgentFullName`, `Description`, `CreatedAt`.

## Dependencies

**Services:** `ApplicationDbContext`, `ProjectScopeService`, `ICurrentUser`.

**Tables written:** none.
**Tables read:** `Leads`, `Projects`, `AspNetUsers`, `ProjectMemberships`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| Projects | `Lead` rows are written by `AddLikedProjectHandler` and deleted by `RemoveLikedProjectHandler`. `Lead.AgentId` is copied from `Immeuble.AgentId`. See `docs/backend/Projects.md`. |
| ProjectMembership | `ProjectScopeService` supplies the project perimeter. |

**Depended on by** — nothing traced.

`Lead.AgentId` is also incremented against `PerformanceIndicator.LeadsGenerated`
by the same favourites handlers, and by
`CreateNotaryAppointmentHandler` — but those write the counter, not `Lead` rows.

## Known edge cases

- Leads are created one per **building** in the liked project, so favouriting a
  project with several immeubles produces several leads for the same user and
  project, differing only by `AgentId`.
- Because `AddLikedProjectHandler` has no duplicate guard, liking the same
  project twice creates a second full set of leads, and un-liking removes only
  the rows matching the single like it found.
- `Lead.CreatedAt` is written with `DateTime.Now` (local time) by the creating
  handler.
- The list has no date-range filter, and no endpoint exposes a single lead by id.

## Frontend coverage

**No frontend caller.** A search for `/api/Leads` across
`realestateFront/src/` returns no matches, and there is no adapter file for this
controller.

The write side is exercised from the UI — `features/buyer/BuyerFavoritesPage.tsx`
via `favoritesApi` — so `Lead` rows accumulate through normal use while nothing
in the frontend reads them back.
