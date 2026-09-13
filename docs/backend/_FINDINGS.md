# Findings index

Cross-cutting and per-module issues surfaced while tracing the code for
`docs/backend/`. Every entry was verified against the statements, not the
comments. Severity is my assessment, not the client's.

**Status: in progress** — covers the modules documented so far
(Reservations, Projects). Updated as each module doc lands.

## Cross-cutting

| # | Severity | Finding |
|---|---|---|
| X1 | High | **`BaseRepository` commits inside every write.** `InsertAsync`, `Update`, `DeleteAsync` and `Delete` each call `SaveChanges` internally. Any handler without an explicit `BeginTransactionAsync` therefore has **no atomicity** — every repository call is its own commit. `Delete(T)` uses the *synchronous* `SaveChanges()` inside async handlers. Trailing `await _repo.SaveAsync()` calls after write loops are no-ops. |
| X2 | High | **Idempotency is implemented but never exercised.** `IdempotencyBehaviour` is registered and `CreateReservationCommand`/`ApproveReservationCommand` implement `IIdempotentRequest`, but the frontend never sends an `Idempotency-Key` header — the string appears in the frontend only as a TypeScript literal (`client.ts:28`). Every request short-circuits the behaviour. |
| X3 | Medium | **Only a minority of commands have validators.** FluentValidation runs via `ValidationBehaviour`, but most commands have no `AbstractValidator`. Reservations: 3 validators for 12 endpoints; `CreateReservationCommand` — the main write path — has none. |
| X4 | Medium | **`KeyNotFoundException` maps to 500.** `ApiExceptionFilter` registers `NotFoundException` but not `KeyNotFoundException`; the type-walk reaches `Exception` and returns 500 `INTERNAL_ERROR`. Handlers throwing it produce a 500 where a 404 is intended. |
| X5 | Low | **Validation failures return 422, not 400.** `ApiExceptionFilter.HandleValidationException` returns `Status422UnprocessableEntity`, while many actions declare `[ProducesResponseType(400)]`. Swagger under-reports the real contract. |
| X6 | Low | **Mixed local/UTC timestamps.** `DateTime.Now` in `CreateReservationHandler.ReservationDate`, `AddLikedProjectHandler.LikedAt`, `Lead.CreatedAt`, `CreateEspaceTempsReelHandler.InsertedAt`; `DateTime.UtcNow` everywhere else on the same entities. |
| X7 | Info | **`Unit.ProjectId` holds the Immeuble id**, not the project id. The real project is `unit.ProjectId → Immeuble.Id → Immeuble.ProjectId`. Confirmed in `GetProjectByIdHandler` and `CreateReservationHandler`. |
| X8 | Info | **Status persistence is not uniform.** `Reservation.Status` is an `int` column; `Unit.Status` is an UPPER_SNAKE `varchar` with a `CK_Units_Status` check constraint. Both appear in the same payloads, which is why the frontend needs both `fromInt` and `fromLegacy` mappers. |
| X9 | Info | **Dapper is a dead dependency.** Referenced in both Infrastructure `.csproj` files; no `.cs` file uses it. All access is EF Core. |
| X10 | Info | **No SignalR anywhere.** No hubs, no `IHubContext`, no package reference. There are no realtime broadcasts in this solution. |

## Reservations

| # | Severity | Finding |
|---|---|---|
| R1 | High | `assign-notaire` has no state-machine guard and no role check — a notary can be attached to a `Rejected`/`Expired`/`Cancelled`/`Sold` reservation, and any user id is accepted without verifying the `NOTARY` role. |
| R2 | Medium | `ValidatedBy` on **reject** is taken from the client-supplied `AdminUserId`. **Approve** deliberately overrides it with the token identity; reject was not given the same fix. The frontend supplies it from `localStorage`. |
| R3 | Medium | Resubmitting overwrites `AdminNote`, destroying the admin's stated reason for requesting changes. |
| R4 | Medium | Co-buyer ownership percentages are never enforced to 100 %. The code rejects only sums **over** 100, and only when every co-buyer supplied a value. The claimed ±0.01 tolerance does not exist. |
| R5 | Medium | Cancellation records no reason and no actor: `CancelReservationCommand` carries only an id, and the `UnitStatusHistory` row is written with `actorUserId: null, reason: null`. |
| R6 | Medium | `POST {id}/documents` throws `KeyNotFoundException` for a missing reservation ⇒ **500** (see X4). |
| R7 | Low | The buyer-ownership check in `GET {id}` is unreachable — the action is `[Authorize(Roles = AdminsAgents)]`, so no buyer reaches the handler. |
| R8 | Low | `POST create`'s `catch (ValidationException)` is dead code; were it reachable the controller would still return **201 Created** with `ReservationId = Guid.Empty`. |
| R9 | Low | `DELETE documents/{id}` returns **400 plain text** for a missing document rather than a `problem+json` 404. |
| R10 | Low | `ApproveReservationCommand.Documents[].Url` is stored verbatim with no validation that it points at project blob storage. Not reachable from the current UI, which always sends `documents: []`. |
| R11 | Low | `ExpiresAt` is never extended by `request-changes`, and the expiry job treats `CHANGES_REQUESTED` as expirable — a file can expire while the agent is correcting it. |
| R12 | Low | Frontend `list` filters status client-side after server pagination, so paging through a status filter skips rows. |
| R13 | Low | Frontend `getMine` maps `finalPrice` onto `reservationAmount`; the buyer portal shows the total price where the admin table shows the deposit. |

## Projects

| # | Severity | Finding |
|---|---|---|
| P1 | **Critical** | **Favourites IDOR cluster.** `POST Like`, `DELETE DisLikeProject`, `GET LikedProjects`, `PUT LikedProject` have no role attribute and take `UserId` from the request. No ownership check exists. `GET LikedProjects?UserId=X` discloses another user's `UserName`, `FirstName`, `LastName`. `PUT LikedProject` lets any authenticated caller repoint any other user's favourite. |
| P2 | High | **`DELETE {projectId}` is an untransacted hard cascade delete** across appointments, sales, reservations, units, immeubles and the project. A mid-way failure leaves the project partially destroyed with no rollback. |
| P3 | High | **`RemoveProject` returns the full stack trace to the client.** Its `catch (Exception)` puts `ex.ToString()` in `Details` and returns 400, bypassing `ApiExceptionFilter`'s "never leak stack traces" rule. |
| P4 | Medium | **`COMPLETED` gate is bypassable at creation.** `UpdateProjectHandler` refuses `StatusGlobal = COMPLETED` (409, directing to the construction complete command), but `CreateProjectHandler` writes it straight through. |
| P5 | Medium | `UpdateLikedProjectHandler` still runs `_ = _repository.Update(x); await SaveAsync();` — the exact un-awaited-update race that `UpdateProjectHandler`'s own comment documents as a 500 and fixes there. |
| P6 | Medium | **Liking is not idempotent.** No duplicate check: liking twice double-increments `NumberLikes`, duplicates `Lead` rows and double-increments `PerformanceIndicator.LeadsGenerated`. Unlike removes only `FirstOrDefault()`, so counters cannot be walked back. |
| P7 | Medium | `GetAllProjectsHandler` calls `tb.ImagesInterieur!.Split(',')` with no null check ⇒ `NullReferenceException`/500 on the **public** catalogue. The sibling `GetTypeBiensByProjectHandler` does null-check. |
| P8 | Medium | **Scoped list pagination is wrong.** The `ProjectMembership` filter is applied after `GetProjects` has paginated, then `Skip`/`Take` runs again; `totalItems` is the post-filter count of an already-paged result. |
| P9 | Low | `GET {id}` loads the project before the perimeter check, so 403-vs-404 distinguishes existence across the perimeter. |
| P10 | Low | Inline quartier creation in both create and update never de-duplicates by name; repeated edits accumulate duplicate quartiers. |
| P11 | Low | `AddProjectFeatureHandler` dereferences `request.Features` with no null guard and no validator ⇒ 500 on a body omitting it. |
| P12 | Low | The commented-out claim-reading block (`ProjectController.cs:52-53`) means `GetAllProjectsQuery.UserId` is caller-supplied; it drives the `IsLiked` flag, so any caller can ask "is this liked by user X". |
| P13 | Low | **10 of 23 endpoints have no frontend caller** (one deliberately). Three more have response-shape mismatches where the frontend reads fields the backend never sends — `create`, `update`, `listQuartiers`. |

## Immeuble

| # | Severity | Finding |
|---|---|---|
| I1 | **Critical** | **`GET /api/Immeuble/floors/{floorId}/units` discloses buyer and agent full names to anonymous callers.** The handler's own comment justifies `[AllowAnonymous]` by claiming enrichment happens "only for units already in a non-selectable state (RESERVED and beyond)". The code applies **no unit-status filter** — it selects reservations where `Status != Rejected && Status != Cancelled`, which still includes `Draft`, `Pending`/SUBMITTED, `ChangesRequested` and **`Expired`**. Buyer names leak for units that are still AVAILABLE or whose reservation already expired. |
| I2 | High | `DeleteImmeublesHandler` **injects `IAppointmentRepository` and never uses it** — the comment sequence skips step 2. Deleting a building orphans every appointment on its units, though `RemoveProjectHandler` does delete them. |
| I3 | High | Both delete paths (building and project) are untransacted; per X1 each `Delete` commits individually, so a mid-way failure leaves sales deleted and units intact. |
| I4 | Medium | `GetAllImmeubles` wraps `Send` in `try/catch` and returns `BadRequest(ex.Message)`, bypassing `ApiExceptionFilter` — scope denials, not-founds and null-refs all surface as an identical 400 carrying an internal message. |
| I5 | Medium | **Building features are dead end-to-end.** `GET features` is called with no `ImmeubleId` (`immeublesApi.ts:140`), binding `Guid.Empty` ⇒ always 404 ⇒ always `[]`. `POST features` has no caller at all. |
| I6 | Medium | `UpdateImmeubleHandler` guards updates on truthiness (`> 0`, `!= 0`, `!IsNullOrWhiteSpace`), so a building cannot be set to latitude 0, price 0, or have text cleared. `UpdateUnitHandler` uses `.HasValue`/`!= null` for the same pattern — inconsistent. |
| I7 | Medium | `UpdateImmeubleHandler` calls `immeuble.Images.Split(',')` with no null guard ⇒ 500; the two read handlers null-check the same field. |
| I8 | Low | `CreateImmeubleHandler` throws a bare `Exception` for a missing project ⇒ 500 instead of 404. |
| I9 | Low | `CreateImmeubleHandler` hardcodes `Status = "ComingSoon"` without `ProjectStatusCodes.Normalize`, so the stored value is the legacy spelling while every read normalises on the way out. |
| I10 | Low | `GET features` and `GET tracking` return 404 for an empty list. `ProjectsController.GetProjectFeatures` was fixed to return `Ok([])` for this exact reason; the fix was not applied here. |
| I11 | Low | `PUT update/{id}` returns a plain-text body (frontend needs `projectFetchText`) and returns **400**, not 404, for a missing unit. |
| I12 | Info | Unit prices (`LatestPrice`, `PriceSaleableValue`, `SaleableValue`) are returned by three `[AllowAnonymous]` endpoints — the full per-unit price list of every project is readable unauthenticated. Presumably intended, but worth an explicit decision. |
| I13 | Info | The stored stock counters on `Immeuble` are written **only** by `CreateImmeubleHandler`, from client input, and never read back — all read paths compute live from `Unit.Status`. |

## Security summary

Ordered by what I would fix first:

1. **I1** — buyer and agent full names disclosed to **anonymous** callers on
   `GET /api/Immeuble/floors/{floorId}/units`, including for draft, pending and
   expired reservations. No authentication required at all.
2. **P1** — favourites IDOR with PII disclosure. Any logged-in account can
   enumerate another user's favourites together with their name.
3. **P3** — full stack traces returned to clients on the project-delete path.
4. **R1** — notary assignment with no role check and no state guard.
5. **P2 / I3** — untransacted cascade deletes at both project and building level.
6. **R2** — forgeable rejection attribution (`ValidatedBy` from the request body).

Repo-level items already noted outside this exercise: the JWT signing key and
OTP secret are committed in both `appsettings.json` files, and CORS is
`AllowAnyOrigin` in the non-development pipeline.
