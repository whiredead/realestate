# Project Agent Assignment Config

`src/ProjectAPI/src/Api/Controllers/ProjectAgentAssignmentConfigController.cs` — class `ProjectAgentAssignmentConfigController`, route prefix **`api/projects/{projectId:guid}/agent-assignment-config`**.

## Purpose

Per-project configuration for how a sales agent is chosen automatically when a
visitor books a commercial appointment without naming one. The stored config
drives `SalesAgentAssignmentService`, which
`CreateAppointmentHandler` calls on its self-service path.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| GET | `/api/projects/{projectId:guid}/agent-assignment-config` | `GetProjectAgentAssignmentConfigHandler` | `GetProjectAgentAssignmentConfigResponse?` |
| PUT | `/api/projects/{projectId:guid}/agent-assignment-config` | `SetProjectAgentAssignmentConfigHandler` | `SetProjectAgentAssignmentConfigResponse` |

Both handler and query/command types are declared in single files under
`Api/Application/ProjectAgentAssignmentConfig/`.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.Admins)]` — `GLOBAL_ADMIN` and
`PROJECT_ADMIN`. `SetProjectAgentAssignmentConfigHandler` additionally applies
the caller's project perimeter.

## Configuration

`ProjectAgentAssignmentConfig` stores a `RuleType` and, for one rule type, a
`PrimaryAgentId`.

`SetProjectAgentAssignmentConfigHandler`:

- rejects a `RuleType` outside `ValidRuleTypes` with `ArgumentException`;
- requires `PrimaryAgentId` when `RuleType == "PRIMARY_AGENT"`, else
  `ArgumentException`;
- calls `EnsureActiveSalesAgentAsync(projectId, primaryAgentId)` — the named
  agent must hold an active `SALES_AGENT` `ProjectMembership` on that project,
  else `BusinessRuleException`;
- clears `PrimaryAgentId` to null whenever `RuleType` is not `PRIMARY_AGENT`;
- updates the existing row if present, otherwise inserts one.

## How the config is consumed

`SalesAgentAssignmentService.AssignAsync(projectId, contact, slotStart, slotEnd)`,
called from `CreateAppointmentHandler` when no `AgentId` was supplied:

1. **Existing owner first.** If `contact.OwnerSalesAgentId` is set, that agent is
   returned with `AssignmentSource = "EXISTING_OWNER"` — the configured rule is
   not consulted.
2. Otherwise the eligible agent set is resolved, and the stored `RuleType`
   selects a strategy:

| `RuleType` | Method | `AssignmentSource` written |
|---|---|---|
| `PRIMARY_AGENT` | `AssignPrimaryAgentAsync` | `PRIMARY_AGENT` |
| `ROUND_ROBIN` | `AssignRoundRobinAsync` | `ROUND_ROBIN` |
| `LOWEST_WORKLOAD` | `AssignLowestWorkloadAsync` | `LOWEST_WORKLOAD` |

`AssignPrimaryAgentAsync` uses the configured agent only when they are in the
eligible set **and** `IsAvailableAsync(agent, slotStart, slotEnd)` returns true;
otherwise it falls through to `AssignLowestWorkloadAsync`. So a
`PRIMARY_AGENT` configuration can yield an appointment whose `AssignmentSource`
is `LOWEST_WORKLOAD`.

The resulting `AgentId` and `AssignmentSource` are written onto the
`Appointment` and onto an `AppointmentAssignmentHistory` row — see
`docs/backend/Appointments.md`.

## Dependencies

**Services:** `ApplicationDbContext`, `ProjectScopeService`, and — on the
consumption side — `ISalesAgentAssignmentService`.

**Tables written:** `ProjectAgentAssignmentConfigs`.
**Tables read:** `ProjectAgentAssignmentConfigs`, `ProjectMemberships`.

## Relations

**Depended on by**

| Module | Mechanism |
|---|---|
| Appointments | `CreateAppointmentHandler` → `ISalesAgentAssignmentService.AssignAsync`, which reads this config. |

**Depends on**

| Module | Mechanism |
|---|---|
| ProjectMembership | `EnsureActiveSalesAgentAsync` requires an active `SALES_AGENT` membership; the eligible-agent set is also drawn from memberships. |
| Projects | The config is keyed on `ProjectId`. |

## Known edge cases

- `SetProjectAgentAssignmentConfigHandler` throws `ArgumentException` for an
  invalid `RuleType` and for a missing `PrimaryAgentId`. `ArgumentException` is
  not registered in `ApiExceptionFilter`, so both surface as **500
  `INTERNAL_ERROR`**. The membership check in the same handler throws
  `BusinessRuleException`, which does map correctly.
- The GET returns `null` for a project with no stored config; the controller
  returns it as a 200 with a null body rather than a 404.
- `EXISTING_OWNER` short-circuits before the configured rule, so a project's
  configuration has no effect for a contact who already has an owning agent.
- `PrimaryAgentId` is validated at write time, but nothing re-validates it later:
  if that agent's membership is subsequently ended, the config still names them
  and `AssignPrimaryAgentAsync` silently falls back to lowest-workload.
- No `AbstractValidator` exists for the command.

## Frontend coverage

**Neither endpoint has a frontend caller.** A search for
`agent-assignment-config` across `src/api/` and `src/features/` returns no
matches, and there is no adapter file for this controller.

The configuration is therefore only settable through direct API access, while the
behaviour it controls is exercised on every anonymous appointment booking through
`features/public/property/AppointmentBookingPage.tsx`.
