# Project Membership

`src/ProjectAPI/src/Api/Controllers/ProjectMembershipController.cs` — class `ProjectMembershipController`, route prefix **`api/ProjectMembership`**.

## Purpose

Staffing: which internal user holds which role on which project, for how long.
`ProjectMembership` rows are what `ProjectScopeService` reads to decide every
project-perimeter check in the solution, so this controller is the write side of
the authorization model.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| POST | `/api/ProjectMembership` | `CreateProjectMembershipHandler` | `{ Id, Message }` |
| POST | `/api/ProjectMembership/{id}/end` | `EndProjectMembershipHandler` | `{ Success, Message }` |
| GET | `/api/ProjectMembership/{id}` | `GetProjectMembershipByIdHandler` | membership |
| GET | `/api/ProjectMembership` | `GetAllProjectMembershipsHandler` | paginated list |

`GET /` takes `projectId`, `userId`, `roleCode`, `pageNumber` (default 1) and
`pageSize` (default 10) as query parameters, assembled into the query object by
the controller.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.Admins)]` — `GLOBAL_ADMIN` and
`PROJECT_ADMIN` only. `CreateProjectMembershipHandler` and
`EndProjectMembershipHandler` additionally call
`ProjectScopeService.EnsureProjectAccessAsync`, so a `PROJECT_ADMIN` can only
staff projects they are themselves a member of.

## Creation rules

`CreateProjectMembershipHandler`, in order:

1. `EnsureProjectAccessAsync(request.ProjectId)`.
2. `RoleCodes.Normalize(request.RoleCode)` must be in
   `RoleCodes.MembershipRoles` = `SALES_AGENT`, `TECHNICIAN`, `NOTARY`,
   `PROJECT_ADMIN`. Otherwise a `ValidationException` (422).
3. `UserId` must be non-blank.
4. The user must exist (`UserManager.FindByIdAsync`), else 404.
5. **The account must already hold that role.** `UserManager.GetRolesAsync` is
   normalised and must contain the requested `roleCode`, else a
   `ValidationException` with the message that "a project membership can only
   grant project scope for a role the account already has".
6. `ValidFrom` defaults to `UtcNow`; `ValidUntil`, when supplied, must be after
   `ValidFrom`.
7. **Transaction**: every existing active row for the same
   `(ProjectId, UserId, RoleCode)` is set `IsActive = false`, then the new row is
   inserted with `AssignedByUserId = ICurrentUser.UserId` and
   `AssignedAt = now`. One `SaveChangesAsync`, then commit.

Step 5 is what stops a membership row from conferring a role the account does not
have. Step 7 keeps at most one active row per
`(ProjectId, UserId, RoleCode)`; the handler's comment names a filtered unique
index `IX_ProjectMemberships_ActivePerUserProjectRole` as the database-level
backstop.

`AssignedByUserId` is taken from the token, not from the request.

## Ending a membership

`EndProjectMembershipHandler` loads the row (404 if absent), checks the project
perimeter, sets `IsActive = false`, and saves. The row is never deleted — the
controller documents this as retention for audit history. `ValidUntil` is not
written, so a membership ended this way keeps whatever validity window it had.

## How the perimeter reads these rows

`ProjectScopeService.GetScopedProjectIdsAsync`:

```csharp
if (_user.IsGlobalAdmin) return null;          // null means unrestricted
…
.Where(m => m.UserId == userId
         && m.IsActive
         && m.ValidFrom <= now
         && (m.ValidUntil == null || m.ValidUntil > now))
.Select(m => m.ProjectId).Distinct()
```

Both conditions must hold independently: `IsActive` is the administrative
switch, the window is time-based. A row that is active but outside its window
grants nothing, and vice versa.

Note this read does **not** filter by `RoleCode` — any active in-window
membership of any role grants the project perimeter. Role-specific checks are
done separately by the callers that need them
(`UpdateAppointmentStatusHandler.EnsureEligibleSalesAgentAsync` requires
`RoleCode == SALES_AGENT`; `EnsureEligibleNotaryAsync` in the notary handlers
requires `RoleCode == NOTARY`).

## Dependencies

**Services:** `ApplicationDbContext`, `IProjectMembershipRepository`,
`UserManager<User>`, `ProjectScopeService`, `ICurrentUser`.

**Tables written:** `ProjectMemberships`.
**Tables read:** `ProjectMemberships`, `AspNetUsers`, `AspNetUserRoles`.

## Relations

**Depended on by** — every project-scoped endpoint in ProjectAPI, through
`ProjectScopeService`. Additionally, three handlers query the table directly for
role-specific eligibility:

| Module | Query |
|---|---|
| Appointments | `EnsureEligibleSalesAgentAsync` — active `SALES_AGENT` membership on the appointment's project |
| NotaryAppointments | `EnsureEligibleNotaryAsync` — active `NOTARY` membership on the reservation's unit's project (in both create and update handlers) |

**Depends on** — the Identity store, via `UserManager`, to verify the account
already holds the requested role.

**Shares its table with** `ProjectAssignmentController` — see
`docs/backend/ProjectAssignment.md`. All six handlers behind that controller
write `ProjectMembership` rows too.

## Known edge cases

- `EndProjectMembership` clears `IsActive` but leaves `ValidUntil` unchanged, so
  the row's stored window still claims a validity it no longer has. The
  perimeter read requires both, so access is correctly revoked; only the stored
  data is inconsistent.
- There is no update endpoint. Changing a validity window means creating a new
  membership, which deactivates the previous one.
- `GetAll` and `GetById` carry no perimeter check, so any admin can read
  memberships for projects they are not on. Only the two writes are scoped.
- The perimeter read ignores `RoleCode`, so a `TECHNICIAN` membership grants the
  same project scope as a `SALES_AGENT` one for every check that does not
  separately test the role.
- `RoleCodes.MembershipRoles` excludes `GLOBAL_ADMIN`, `BUYER`, `PROSPECT` and
  `VISITOR`; a membership cannot be created for those.
- No `AbstractValidator` exists; all rules are inline and throw the
  application's `ValidationException`.

## Frontend coverage

`realestateFront/src/api/http/projectMembershipsApi.ts`; UI in
`features/assignments/AssignmentsPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| GET `/` | `projectMembershipsApi.list` |
| GET `{id}` | `projectMembershipsApi.getById` |
| POST `/` | `projectMembershipsApi.create` |
| POST `{id}/end` | `projectMembershipsApi.end` |

All four endpoints are called. `create` types the response as
`{ id: string; message: string }` and `end` as `{ success: boolean; message: string }`,
both matching the handler responses.
