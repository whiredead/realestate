# Agent Availability

`src/ProjectAPI/src/Api/Controllers/AgentAvailabilityController.cs` — class `AgentAvailabilityController`, route prefix **`api/agents/{agentId}`**.

The agent id is a route-template segment on the controller, so it appears in
every path and is bound as a `string` (Identity user ids are strings).

## Purpose

A sales agent's availability calendar, made up of four independent pieces:

- a recurring **weekly template** (`AgentWeeklyAvailability`),
- ad-hoc **blocks** for leave or closures (`AgentBlock`),
- whole-day **date overrides** (`AgentDateOverride`),
- **slot settings** — duration and buffer (`AgentAppointmentSettings`),

plus a computed read that composes all four into bookable slots.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| POST | `/api/agents/{agentId}/blocks` | `CreateAgentBlockHandler` | `CreateAgentBlockResponse` — 201 |
| DELETE | `/api/agents/{agentId}/blocks/{blockId:guid}` | `DeleteAgentBlockHandler` | 204 or 404 |
| GET | `/api/agents/{agentId}/blocks` | `GetAgentBlocksHandler` | `List<AgentBlockDto>` |
| PUT | `/api/agents/{agentId}/weekly-availability` | `SetAgentWeeklyAvailabilityHandler` | the submitted slots |
| GET | `/api/agents/{agentId}/weekly-availability` | `GetAgentWeeklyAvailabilityHandler` | weekly slots |
| POST | `/api/agents/{agentId}/date-overrides` | `SetAgentDateOverrideHandler` | override — 201 |
| DELETE | `/api/agents/{agentId}/date-overrides/{overrideId:guid}` | `DeleteAgentDateOverrideHandler` | 204 or 404 |
| PUT | `/api/agents/{agentId}/appointment-settings` | `SetAgentAppointmentSettingsHandler` | settings |
| GET | `/api/agents/{agentId}/available-slots` | `GetAgentAvailableSlotsHandler` | `List<AgentDayAvailabilityDto>` |

`GET blocks` takes optional `from`/`to` query parameters; `GET available-slots`
takes required `from`/`to`.

The two `CreatedAtAction` calls point at read actions:
`CreateBlock` → `GetBlocks`, `CreateDateOverride` → `GetWeeklyAvailability`.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.AdminsAgents)]` —
`GLOBAL_ADMIN`, `PROJECT_ADMIN`, `SALES_AGENT`. No per-action attributes.

Eight of the nine handlers call
`ProjectScopeService.EnsureAgentOwnsCalendar(agentId)`, which is synchronous and
does no database work:

```csharp
if (_user.IsGlobalAdmin || _user.IsInRole(RoleCodes.ProjectAdmin)) return;
if (_user.IsInRole(RoleCodes.SalesAgent) && !string.Equals(_user.UserId, targetAgentId, StringComparison.Ordinal))
    throw BusinessRuleException.AgentCalendarScopeDenied();
```

So a `GLOBAL_ADMIN` or `PROJECT_ADMIN` may manage any agent's calendar, and a
`SALES_AGENT` only their own. There is **no project-perimeter check** — a
`PROJECT_ADMIN` is not restricted to agents on their own projects.

`GetAgentAvailableSlotsHandler` is the one handler that does **not** call it, so
any caller in `AdminsAgents` can read any agent's computed slots.

## Slot computation

`GetAgentAvailableSlotsHandler` iterates day by day from `From.Date` to
`To.Date` inclusive.

**Day template.** A date override covering the day replaces the weekly template
entirely:

| Override `Status` | Template for the day |
|---|---|
| `"Unavailable"` | empty — no slots |
| anything else | one window `00:00`–`24:00` |
| (no override) | the `AgentWeeklyAvailability` rows whose `DayOfWeek` matches |

Only the **first** matching override is used (`FirstOrDefault`), and the
comparison is on `Status == "Unavailable"` as a literal string.

**Busy windows subtracted from each template window:**

- every `AgentBlock` overlapping the window, clipped to it;
- every `Appointment` for the agent whose `Status` is `Requested`, `Confirmed`
  or `RescheduleProposed`, treated as occupying `AppointmentDate` to
  `AppointmentDate + (duration + buffer)`.

`SubtractWindows` removes the busy intervals and returns the remaining free
intervals.

**Slicing.** Each free interval is cut into slots of `DurationMinutes`, advancing
the cursor by `DurationMinutes + BufferMinutes` each time, while
`cursor + duration <= intervalEnd`. Settings come from
`AgentAppointmentSettings` for the agent, falling back to the constants
`DefaultDurationMinutes = 30` and `DefaultBufferMinutes = 0`.

The handler reads `Appointment` rows only — it does not consider
`FinalVisitAppointment` or `HandoverAppointment`, which are separate tables.

## Block creation

`CreateAgentBlockHandler`:

1. `EnsureAgentOwnsCalendar(req.AgentId)` — the id comes from the **body** here
   (the controller assigns the route value onto it first).
2. `End <= Start` ⇒ `ArgumentException`.
3. `Start`/`End` are re-tagged with `DateTimeKind.Utc` via
   `DateTime.SpecifyKind` — the incoming values are not converted, only
   relabelled.
4. Rejects the block when any appointment for the agent falls inside it whose
   status is **not** `Cancelled`, `Rejected`, `Completed`, `NoShow` or
   `Superseded` — i.e. the complement of the terminal statuses, which is wider
   than `AppointmentStateMachine.BlockingStatuses`. Throws
   `InvalidOperationException` listing the conflicting times.

## Weekly availability

`SetAgentWeeklyAvailabilityHandler` is replace-all: it validates every slot has
`EndTime > StartTime` (else `ArgumentException`), deletes all existing rows for
the agent, inserts the submitted set, and returns the request's slots unchanged.

Because `BaseRepository.Delete` and `InsertAsync` each commit, the delete and the
re-insert are separate transactions.

## Dependencies

**Services:** `IAgentWeeklyAvailabilityRepository`, `IAgentBlockRepository`,
`IAgentDateOverrideRepository`, `IAgentAppointmentSettingsRepository`,
`IAppointmentRepository`, `ProjectScopeService`.

**Tables written:** `AgentBlocks`, `AgentWeeklyAvailabilities`,
`AgentDateOverrides`, `AgentAppointmentSettings`.
**Tables read:** the above plus `Appointments`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| Appointments | Reads `Appointments` to subtract busy time and to reject overlapping blocks. See `docs/backend/Appointments.md`. |
| ProjectMembership | Only through `ProjectScopeService.EnsureAgentOwnsCalendar`, which reads no membership rows. |

**Depended on by** — nothing traced. `CreateAppointmentHandler` and
`UpdateAppointmentStatusHandler` perform their own ±1-minute conflict check
against `Appointments` directly and do **not** consult `AgentBlock`,
`AgentWeeklyAvailability`, `AgentDateOverride` or `AgentAppointmentSettings`.

`NotaryBlocksController` covers the same four concepts for notaries against
different tables; `UpdateNotaryAppointmentHandler` does check `NotaryBlock`.

## Known edge cases

- **The availability calendar does not constrain booking.** Nothing in
  `Appointments` reads these tables, so an appointment can be created inside a
  block, outside the weekly template, on an `Unavailable` override day, or at a
  time not on the configured slot grid. The relationship runs one way: blocks
  refuse to be created over appointments, but appointments do not refuse to be
  created over blocks.
- `GetAgentAvailableSlots` has no ownership check, so any agent may read any
  other agent's calendar as computed slots.
- No project-perimeter check anywhere in this controller: a `PROJECT_ADMIN` can
  manage the calendar of an agent who works on no project of theirs.
- `CreateAgentBlockHandler` throws `ArgumentException` and
  `InvalidOperationException`, and `SetAgentWeeklyAvailabilityHandler` throws
  `ArgumentException`. Neither type is registered in `ApiExceptionFilter`, so
  both produce **500 `INTERNAL_ERROR`** rather than 4xx.
- `DateTime.SpecifyKind(..., Utc)` relabels the incoming value without
  converting it, so a client sending local times has them stored as if UTC.
- The block-conflict check enumerates statuses to exclude rather than using
  `AppointmentStateMachine.BlockingStatuses`; `Superseded` is excluded here but
  is also absent from `BlockingStatuses`, so the two sets differ only in that
  this one has no positive definition.
- A date override's `Status` is compared to the literal `"Unavailable"`; any
  other value, including a typo, opens the full 24-hour day.
- Overlapping date overrides are possible; only the first match is applied.
- Weekly availability replace-all is not transactional, so a failure between the
  delete and the insert leaves the agent with no weekly template.
- No `AbstractValidator` exists for any command in this module.

## Frontend coverage

`realestateFront/src/api/http/agentAvailabilityApi.ts`; UI in
`features/assignments/AgentAvailabilityPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| GET `blocks` | `agentAvailabilityApi.listBlocks` |
| POST `blocks` | `agentAvailabilityApi.createBlock` |
| DELETE `blocks/{blockId}` | `agentAvailabilityApi.deleteBlock` |
| GET `weekly-availability` | `agentAvailabilityApi.getWeeklyAvailability` |
| PUT `weekly-availability` | `agentAvailabilityApi.setWeeklyAvailability` |
| POST `date-overrides` | `agentAvailabilityApi.createDateOverride` |
| DELETE `date-overrides/{overrideId}` | `agentAvailabilityApi.deleteDateOverride` |
| PUT `appointment-settings` | `agentAvailabilityApi.setAppointmentSettings` |
| GET `available-slots` | **no caller** |

Eight of the nine endpoints are called. A repository-wide search for
`available-slots` in `src/api/` and `src/features/` returns no match, so the slot
computation described above is not consumed by this frontend.

There is no `GET appointment-settings` endpoint, so the frontend can write slot
settings but cannot read back what is stored.
