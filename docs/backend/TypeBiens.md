# TypeBiens

`src/ProjectAPI/src/Api/Controllers/TypeBiensController.cs` — class `TypeBiensController`, route prefix **`api/TypeBiens`**.

## Purpose

Write-side CRUD for `TypeBien`, the property-type reference data (name,
description, image, price, bedroom/bathroom counts, surface range, interior
images).

`TypeBien` has an **`int`** primary key, unlike almost every other entity in the
solution, which uses `Guid`.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| POST | `/api/TypeBiens` | `CreateTypeBienHandler` | `{ Id, Message }` |
| PUT | `/api/TypeBiens/{id}` | `UpdateTypeBienHandler` | `UpdateTypeBienResponse` |
| DELETE | `/api/TypeBiens/{id}` | `DeleteTypeBienHandler` | `DeleteTypeBienResponse` |

There is **no read endpoint on this controller.** Reads live on
`ProjectsController`:

- `GET /api/Projects/type-biens` — all types
- `GET /api/Projects/{projectId}/type-biens` — types linked to one project

Both are served by `GetTypeBiensByProjectHandler`, documented in
`docs/backend/Projects.md`.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.Admins)]` — `GLOBAL_ADMIN` and
`PROJECT_ADMIN`. No per-action attributes and no `ProjectScopeService` call in
any handler: `TypeBien` is global reference data with no project column, so
there is no perimeter to apply.

The two read endpoints on `ProjectsController` are `[AllowAnonymous]`.

## Behaviour

**Create** — maps the command onto a new `TypeBien` and inserts it. The `Id` is
database-generated and returned. No uniqueness check on `Name`.

**Update** — the controller returns 400 when the route `id` does not match
`command.Id`, then maps the handler's `IsSuccess` to `Ok(response)` or
`BadRequest(response)`.

**Delete** — `DeleteTypeBienHandler` refuses when the type is still referenced,
checking two things in order:

1. `ProjectTypeBien` rows with this `TypeBienId`. If any exist the handler
   returns `IsSuccess = false` with the referencing project names.
2. `Appointment` rows whose `TypeBienIds` collection contains this id. If any
   exist it likewise returns `IsSuccess = false`.

Only when both are empty is the row deleted. Both checks call `GetAllAsync()` on
the respective table and filter in memory.

`ImmeubleTypeBien` exists as an entity but is not among the references checked
here.

Because the handler returns a failure object rather than throwing, the controller
maps every refusal to **400**, including the two referential-integrity cases.

## Dependencies

**Services:** `ITypeBienRepository`, plus the repositories/context used by the
delete handler to read `ProjectTypeBiens` and `Appointments`.

**Tables written:** `TypeBiens`.
**Tables read:** `TypeBiens`, `ProjectTypeBiens`, `Appointments`.

## Relations

**Depended on by**

| Module | Mechanism |
|---|---|
| Projects | `POST /api/Projects/{projectId}/type-biens` creates the `ProjectTypeBien` link; `GetAllProjectsHandler` and `GetTypeBiensByProjectHandler` project `TypeBien` fields into their responses. See `docs/backend/Projects.md`. |
| Appointments | `Appointment.TypeBienIds` is a collection of `int` ids indicating which property types a visitor asked about. See `docs/backend/Appointments.md`. |
| Immeuble | `ImmeubleTypeBien` and `AssociateTypeBienToImmeubleHandler` exist; `GetImmeubleByIdHandler` returns an empty `TypeBiens` list with the comment that types are associated with projects rather than immeubles. |

**Depends on** — nothing.

## Known edge cases

- The controller has no GET, so creating or updating a type gives no way to read
  it back from the same controller; callers use the `ProjectsController` routes.
- Delete refusals are returned as 400 with a message rather than 409, and share
  that status with the id-mismatch validation on update.
- The delete guard does not check `ImmeubleTypeBien`, so a type linked only to an
  immeuble can be deleted.
- Both delete guards load entire tables with `GetAllAsync()` and filter in
  memory.
- `Name` has no uniqueness constraint enforced in the handler.
- `ImagesInterieur` is a single comma-delimited string on the entity. Consumers
  split it: `GetTypeBiensByProjectHandler` null-checks before splitting,
  `GetAllProjectsHandler` does not.
- Only `DeleteTypeBienCommand` and `UpdateTypeBienCommand` have
  `AbstractValidator` classes; `CreateTypeBienCommand` has none.

## Frontend coverage

`realestateFront/src/api/http/typeBiensApi.ts`; UI in
`features/typebiens/TypeBiensPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| POST `/api/TypeBiens` | `typeBiensApi.create` |
| PUT `/api/TypeBiens/{id}` | `typeBiensApi.update` |
| DELETE `/api/TypeBiens/{id}` | `typeBiensApi.remove` |

All three write endpoints are called. `typeBiensApi.list()` calls
`/api/Projects/type-biens` instead, with a comment stating that
`TypeBiensController` has no GET — which matches the controller.

`create` returns `{ id: res.id, ...data }` and `update` returns
`{ id, ...data }`, both assembled locally from the request rather than from the
response body. `remove` types the response as `unknown` and discards it, so a
refusal returned as 400 surfaces as a thrown `ApiError` rather than as a
readable reason.

`projectsApi.listTypeBiens` calls the same `/api/Projects/type-biens` route, so
two adapters reach that endpoint.
