# Notary Blocks and Availability

`src/ProjectAPI/src/Api/Controllers/NotaryBlocksController.cs` — class `NotaryBlocksController`, route prefix **`api/notaries/{notaryId}`**.

## Purpose

A notary's availability calendar: ad-hoc blocks (`NotaryBlock`) and a recurring
weekly template (`WeeklyAvailability`). These are the two tables that
`UpdateNotaryAppointmentHandler` and `GetNotaryCalendarHandler` read when
deciding whether a notarial slot is free.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| POST | `/api/notaries/{notaryId}/blocks` | `CreateNotaryBlockHandler` | `CreateNotaryBlockResponse` — 201 |
| DELETE | `/api/notaries/{notaryId}/blocks/{blockId:guid}` | `DeleteNotaryBlockHandler` | 204 or 404 |
| GET | `/api/notaries/{notaryId}/blocks` | `GetNotaryBlocksHandler` | `List<NotaryBlockDto>` |
| PUT | `/api/notaries/{notaryId}/weekly-availability` | `SetNotaryWeeklyAvailabilityHandler` | the submitted slots |
| GET | `/api/notaries/{notaryId}/weekly-availability` | `GetNotaryWeeklyAvailabilityHandler` | weekly slots |

`GET blocks` takes optional `from`/`to` query parameters.

**The POST does not bind the route segment.** `Create` reads only
`[FromBody] CreateNotaryBlockCommand` and never assigns `notaryId` onto it; it
then uses `body.NotaryId` in the `CreatedAtAction` route values. The `{notaryId}`
path segment is therefore ignored on this action, and the body's `NotaryId` is
what the handler acts on. Every other action in the controller assigns or passes
the route value. `AgentAvailabilityController.CreateBlock` does
`body.AgentId = agentId`; this one does not.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.AdminsNotary)]` —
`GLOBAL_ADMIN`, `PROJECT_ADMIN`, `NOTARY`. No per-action attributes.

All five handlers call `ProjectScopeService.EnsureNotaryOwnsCalendar(notaryId)`:

```csharp
if (_user.IsGlobalAdmin || _user.IsInRole(RoleCodes.ProjectAdmin)) return;
if (_user.IsInRole(RoleCodes.Notary) && !string.Equals(_user.UserId, targetNotaryId, StringComparison.Ordinal))
    throw BusinessRuleException.NotaryCalendarScopeDenied();
```

Synchronous, reads no database rows. Admins may manage any notary's calendar; a
`NOTARY` only their own. There is no project-perimeter check.

`SetNotaryWeeklyAvailabilityHandler` additionally rejects an empty `NotaryId`
with the application's `ValidationException` (422) before the ownership check.

## Block creation

`CreateNotaryBlockHandler`:

1. `EnsureNotaryOwnsCalendar(req.NotaryId)` — from the body, see above.
2. `End <= Start` ⇒ `ArgumentException`.
3. `Start`/`End` relabelled with `DateTime.SpecifyKind(..., Utc)` — no
   conversion, only a kind change.
4. Rejects the block when **any** `NotaryAppointment` for that notary falls in
   `[Start, End)`. The filter is on date range and `NotaireId` only — unlike the
   agent equivalent, it does **not** exclude terminal statuses, so a cancelled,
   rejected or completed appointment still prevents the block.
   Throws `InvalidOperationException` listing the conflicting times.

## Weekly availability

`SetNotaryWeeklyAvailabilityHandler` is replace-all: validates every slot has
`EndTime > StartTime` (else `ArgumentException`), deletes all `WeeklyAvailability`
rows for the notary, inserts the submitted set, returns the request's slots.

Because `BaseRepository.Delete` and `InsertAsync` each commit, the delete and the
re-insert are separate transactions.

The entity is named `WeeklyAvailability` (notary side) as against
`AgentWeeklyAvailability` (agent side); they are separate tables.

## Related read — `GetNotaryCalendarHandler`

Not exposed by this controller. It backs
`GET /api/NotaryAppointments/NotaireAvailability` (see
`docs/backend/NotaryAppointments.md`) and reads the same two tables.

Its composition differs from the agent equivalent:

| | Notary calendar | Agent available-slots |
|---|---|---|
| Weekly template | yes | yes |
| Ad-hoc blocks subtracted | yes | yes |
| Appointments subtracted as | a flat **1-minute** window (`dt` → `dt.AddMinutes(1)`) | `duration + buffer` |
| Date overrides | not read | `AgentDateOverride` |
| Slot duration / buffer | none | `AgentAppointmentSettings` |
| Output | free intervals | discrete sliced slots |

`NotaryDateDisponibilite` and its repository interface exist in the Domain layer,
and `CreateDateDisponibiliteHandler` exists under
`Api/Application/Notary/DatesDisponibilites/`, but no controller action dispatches
`CreateDatesDisponibiliteCommand`, and `GetNotaryCalendarHandler` does not read
that entity.

## Dependencies

**Services:** `INotaryBlockRepository`, `IWeeklyAvailabilityRepository`,
`INotaryAppointmentRepository`, `ProjectScopeService`.

**Tables written:** `NotaryBlocks`, `WeeklyAvailabilities`.
**Tables read:** the above plus `NotaryAppointments`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| NotaryAppointments | Reads `NotaryAppointments` to reject a block over an existing appointment. |
| ProjectMembership | Only via `EnsureNotaryOwnsCalendar`, which reads no membership rows. |

**Depended on by**

| Module | Mechanism |
|---|---|
| NotaryAppointments | `UpdateNotaryAppointmentHandler.EnsureSlotStillAvailableAsync` and `CreateNotaryAppointmentHandler.EnsureSlotAvailableAsync` both query `NotaryBlock` for `Start <= appointmentDate < End`. `GetNotaryCalendarHandler` reads both tables. |

This is the difference from the agent side: notary blocks **do** constrain
appointment booking, whereas `AgentBlock` is not read by any appointment handler
(see `docs/backend/AgentAvailability.md`).

## Known edge cases

- `POST blocks` ignores the `{notaryId}` route segment and acts on the body's
  `NotaryId`. The ownership check runs against the body value, so a `NOTARY`
  cannot act on someone else's calendar, but an admin's request path and effect
  can disagree.
- The block-conflict check does not exclude terminal appointment statuses, so a
  cancelled or completed notarial appointment still blocks the creation of a
  `NotaryBlock` over that time.
- `ArgumentException` and `InvalidOperationException` are not registered in
  `ApiExceptionFilter`, so both surface as **500 `INTERNAL_ERROR`**.
- `DateTime.SpecifyKind(..., Utc)` relabels rather than converts, so local times
  sent by a client are stored as if they were UTC.
- Weekly-availability replace-all is not transactional; a failure between the
  delete and the insert leaves the notary with no weekly template.
- No project-perimeter check: a `PROJECT_ADMIN` may manage any notary's calendar.
- `GetNotaryCalendarHandler` treats every appointment as one minute of busy time,
  so a notarial appointment does not reserve its actual duration in the returned
  availability.
- No `AbstractValidator` exists for any command in this module.

## Frontend coverage

`realestateFront/src/api/http/notaryApi.ts`; UI in
`features/notary/NotaryBlocksPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| GET `blocks` | `notaryApi.listBlocks` |
| POST `blocks` | `notaryApi.createBlock` |
| DELETE `blocks/{blockId}` | `notaryApi.deleteBlock` |
| GET `weekly-availability` | `notaryApi.getWeeklyAvailability` |
| PUT `weekly-availability` | `notaryApi.setWeeklyAvailability` |

All five endpoints are called, from the same adapter that serves
`NotaryAppointmentsController`.
