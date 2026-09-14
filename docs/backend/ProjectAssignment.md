# Project Assignment

`src/ProjectAPI/src/Api/Controllers/ProjectAssignmentController.cs` — class `ProjectAssignmentController`, route prefix **`api/ProjectAssignment`**.

## Purpose

An agent-or-notary staffing API. The controller's doc comment describes it as a
legacy compatibility shim superseded by `ProjectMembershipController`.

**All six handlers behind it read and write the `ProjectMembership` entity.** A
repository-wide search shows no handler in `Api/Application/ProjectAssignments/`
references `Set<ProjectAssignment>` or an `IProjectAssignmentRepository`; every
one uses `ProjectMembership` or `IProjectMembershipRepository`. The two
controllers are therefore two shapes over the same table, not two tables.

The `ProjectAssignment` entity itself still exists in the Domain layer and has
EF configurations (`AssignmentConfiguration.cs`,
`ProjectAssignmentConfiguration.cs`), but outside those configuration files and
a doc-comment reference in `ProjectScopeService`, no application code reads or
writes it.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| POST | `/api/ProjectAssignment` | `CreateProjectAssignmentHandler` | `{ Id, Message }` |
| DELETE | `/api/ProjectAssignment/UnassignAgentsNotaire` | `UnassignProjectAssignmentHandler` | 204 or 400 |
| PUT | `/api/ProjectAssignment` | `UpdateProjectAssignmentHandler` | update response |
| DELETE | `/api/ProjectAssignment/{id}` | `DeleteProjectAssignmentHandler` | delete response |
| GET | `/api/ProjectAssignment/{id}` | `GetProjectAssignmentByIdHandler` | assignment |
| GET | `/api/ProjectAssignment` | `GetAllProjectAssignmentsHandler` | paginated list |

`PUT` takes the id from the body, not the route. `DELETE UnassignAgentsNotaire`
takes a body on a DELETE verb. `GET /` accepts `projectId`, `agentId`,
`pageNumber` (default 1), `pageSize` (default 10).

## Authorization

Class-level `[Authorize(Roles = RoleGroups.Admins)]` — `GLOBAL_ADMIN` and
`PROJECT_ADMIN`. `CreateProjectAssignmentHandler` and
`UnassignProjectAssignmentHandler` call
`ProjectScopeService.EnsureProjectAccessAsync`.

## Creation behaviour

`CreateProjectAssignmentHandler`:

1. `EnsureProjectAccessAsync(request.ProjectId)`.
2. Normalises the two id fields, treating both empty and the literal string
   `"string"` as null:
   ```csharp
   var agentId = request.AgentId.IsNullOrEmpty() || request.AgentId == "string" ? null : request.AgentId;
   ```
   (the same expression is applied to `NotaryId`).
3. Requires at least one of `AgentId` / `NotaryId`, else `ValidationException`.
4. `EnsureUserHoldsRoleAsync` for each supplied id — the account must already
   hold `SALES_AGENT` (for `AgentId`) or `NOTARY` (for `NotaryId`), same guard as
   `CreateProjectMembershipHandler`.
5. **Transaction**: for each supplied id, an inner `UpsertAsync` deactivates
   every existing active `ProjectMembership` for that
   `(ProjectId, UserId, RoleCode)` and inserts a new one with
   `ValidFrom = now`, `ValidUntil = null`, `IsActive = request.IsActive`,
   `AssignedByUserId = ICurrentUser.UserId`. One `SaveChangesAsync`, then commit.

When both ids are supplied, two membership rows are created but the response
carries only the **second** `createdId` — `createdId` is assigned by the agent
branch and then overwritten by the notary branch.

`IsActive` is taken from the request, so this endpoint can create a row that is
inactive on creation. `CreateProjectMembershipHandler` always sets
`IsActive = true`.

`ValidUntil` is always `null` here; the validity-window fields are not exposed by
this controller.

## Unassign and delete

`UnassignProjectAssignmentHandler` loads a `ProjectMembership` through
`IProjectMembershipRepository` (404 if absent), checks the project perimeter, and
sets `IsActive = false` — the same effect as
`POST /api/ProjectMembership/{id}/end`.

`DeleteProjectAssignmentHandler` and `UpdateProjectAssignmentHandler` also
operate on `ProjectMembership`.

## Dependencies

**Services:** `ApplicationDbContext`, `IProjectMembershipRepository`,
`UserManager<User>`, `ProjectScopeService`, `ICurrentUser`.

**Tables written:** `ProjectMemberships`.
**Tables read:** `ProjectMemberships`, `AspNetUsers`, `AspNetUserRoles`.

## Relations

**Writes the same table as** `ProjectMembershipController` — see
`docs/backend/ProjectMembership.md` for how those rows drive
`ProjectScopeService` and every project-perimeter check.

**Depends on** the Identity store via `UserManager` for the role-match guard.

**Depended on by** — nothing reads through this controller; consumers read
`ProjectMembership` rows directly or through `ProjectScopeService`.

## Known edge cases

- The controller name, route and DTO vocabulary say "assignment" while the
  storage is `ProjectMembership`. A row created here is visible through
  `GET /api/ProjectMembership` and can be ended by either controller.
- `"string"` is treated as a sentinel for "not supplied" on both `AgentId` and
  `NotaryId`. That is the placeholder value Swagger UI pre-fills, so a request
  sent unmodified from Swagger is read as null rather than as a user id.
- Creating both an agent and a notary in one call returns only the notary's id.
- `IsActive` is caller-controlled, so this endpoint can create a membership that
  grants nothing.
- `PUT` takes its id from the body while `DELETE {id}` takes it from the route.
- `DELETE UnassignAgentsNotaire` requires a request body, which many HTTP clients
  do not send on DELETE by default.
- No validity window can be set or read through this controller, so a membership
  created here is open-ended until deactivated.
- No `AbstractValidator` exists for any of these commands.

## Frontend coverage

`realestateFront/src/api/http/assignmentsApi.ts`; UI in
`features/assignments/AssignmentsPage.tsx`, which also uses
`projectMembershipsApi`.

| Endpoint | Frontend caller |
|---|---|
| GET `/` | `assignmentsApi.list` |
| GET `{id}` | `assignmentsApi.getById` |
| POST `/` | `assignmentsApi.create` |
| PUT `/` | `assignmentsApi.update` |
| DELETE `{id}` | `assignmentsApi.remove` |
| DELETE `UnassignAgentsNotaire` | **no caller** |

Five of the six endpoints are called. `assignmentsApi.update` sends to
`/api/ProjectAssignment` without an id in the path, matching the controller's
body-bound `PUT`.

`assignmentsApi.create` and `update` type the response as
`ProjectAssignmentDto`; the handlers return `{ Id, Message }` shapes, so fields
beyond those are undefined at runtime.
