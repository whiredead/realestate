# Notifications

`src/ProjectAPI/src/Api/Controllers/NotificationsController.cs` — class `NotificationsController`, route prefix **`api/notifications`**.

## Purpose

A per-user notification inbox. Rows are written by other modules through
`INotificationService`; this controller only reads the caller's own rows and
marks them read.

## Endpoints

| Verb | Path | Handler | Request | Response |
|---|---|---|---|---|
| GET | `/api/notifications/mine` | `GetMyNotificationsHandler` | `unreadOnly` (query, default `false`) | `List<NotificationDto>` |
| POST | `/api/notifications/{id:guid}/read` | `MarkNotificationReadHandler` | route id | `bool` |

Class-level `[Authorize]`; no per-action role attributes.

Neither endpoint accepts a user id. `GetMyNotificationsHandler` filters on
`n.UserId == ICurrentUser.UserId` and returns an empty list when the caller has
no id. `MarkNotificationReadHandler` loads the notification by id
(`NotFoundException` if absent) and throws
`BusinessRuleException.ProjectScopeDenied(Guid.Empty)` when
`notification.UserId != ICurrentUser.UserId`.

## Read behaviour

`GetMyNotificationsHandler` orders by `CreatedAt` descending and applies
`.Take(200)`. The limit is fixed in the handler; there is no paging parameter, so
a caller with more than 200 notifications cannot reach the older ones.

`UnreadOnly = true` adds `Where(n => !n.IsRead)`.

The DTO exposes `Id`, `Type`, `Title` (from `TitleFr`), `Body` (from `BodyFr`),
`RelatedEntityId`, `RelatedEntityType`, `IsRead`, `CreatedAt`. The entity stores
French-suffixed columns (`TitleFr`, `BodyFr`); only those are written and read —
no other language column is populated anywhere traced.

## Mark-read behaviour

`MarkNotificationReadHandler` sets `IsRead = true` and `ReadAt = UtcNow` only
when the row is currently unread, then saves. Calling it on an
already-read notification returns `true` without writing. There is no
mark-unread and no bulk mark-all endpoint.

## Write path — `INotificationService`

`Api/Application/Common/Notifications/NotificationService.cs`:

```csharp
if (string.IsNullOrWhiteSpace(userId)) return;
_db.Add(new Notification { Id, UserId, Type, TitleFr, BodyFr,
                           RelatedEntityId, RelatedEntityType, CreatedAt = UtcNow });
await _db.SaveChangesAsync(ct);
```

A blank `userId` is a silent no-op. The method calls `SaveChangesAsync` itself,
so a caller inside a transaction has the insert enlisted in that transaction; a
caller outside one commits immediately. Every traced caller invokes it **after**
its own commit.

### Type codes emitted

| Type | Emitted by |
|---|---|
| `RESERVATION_APPROVED` | `ApproveReservationHandler` — to buyer and agent |
| `RESERVATION_REJECTED` | `RejectReservationHandler` — to agent |
| `RESERVATION_EXPIRED` | `ReservationExpiryJob` — to agent |
| `PAYMENT_RECORDED` | `RecordPaymentHandler` — to buyer |
| `PAYMENT_VALIDATED` | `ValidatePaymentHandler` — to buyer |
| `UNIT_DELIVERED` | `AcknowledgeHandoverHandler` — to buyer |
| `APPOINTMENT_REMINDER` | `AppointmentReminderJob` — to sales agent, buyer, final-visit agent/buyer, handover agent/buyer |
| `SAV_SLA_WARNING`, `SAV_SLA_BREACHED` | `SavSlaDetectionJob` — to the assigned technician |

Other UPPER_SNAKE constants in the codebase with similar shapes
(`RESERVATION_SUBMITTED`, `RESERVATION_CANCELLED`, `UNIT_NOT_AVAILABLE`,
`APPOINTMENT_SLOT_CONFLICT`, `PAYMENT_ALREADY_REVERSED`,
`APPOINTMENT_NOT_COMPLETED`) are `UnitStatusCause` values or
`BusinessErrorCodes`, not notification types.

Modules that write no notifications at all: FinalVisits, Construction,
Deliveries, Claims (the controller — its SLA job does), Projects, Immeuble,
NotaryAppointments.

## Dependencies

**Services:** `ApplicationDbContext`, `ICurrentUser`.
**Tables written:** `Notifications` (`IsRead`, `ReadAt` here; inserts elsewhere).
**Tables read:** `Notifications`.

`NotificationsController` itself depends on no other module. `INotificationService`
is registered as scoped in `Api/Application/DependencyInjection.cs`.

## Relations

**Depended on by** — Reservations, Payments, Handovers, and the
`ReservationExpiryJob`, `AppointmentReminderJob` and `SavSlaDetectionJob` hosted
services, all through `INotificationService.NotifyAsync`.

**Depends on** — nothing. The controller reads only its own table.

`SentReminder` is a separate table used by `InstallmentOverdueJob` and
`SavSlaDetectionJob` for deduplication; it is not read or written here.

## Known edge cases

- The 200-row cap is fixed and unpaged, so older notifications become
  unreachable once a user exceeds it.
- `NotifyAsync` returns silently for a blank `userId`. Several callers pass a
  nullable id (`reservation.AgentId`, `claim.AssignedAgentId`,
  `reservation.BuyerId`), so a notification is skipped rather than failing when
  the recipient is unset — for example a reservation whose buyer has no account.
- `MarkNotificationRead` reuses `BusinessRuleException.ProjectScopeDenied(Guid.Empty)`
  for an ownership failure, so the response carries the `PROJECT_SCOPE_DENIED`
  code and an empty GUID although no project is involved.
- Only `TitleFr`/`BodyFr` are populated; the DTO maps them to
  language-neutral `Title`/`Body`.
- There is no delete, no mark-all-read, and no unread count endpoint.
- Neither command has an `AbstractValidator`.

## Frontend coverage

`realestateFront/src/api/http/notificationsApi.ts`; UI in
`features/buyer/BuyerNotificationsPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| GET `/api/notifications/mine` | `notificationsApi.listMine` |
| POST `/api/notifications/{id}/read` | `notificationsApi.markRead` |

Both endpoints are called. `listMine` appends `?unreadOnly=true` only when the
flag is set. `markRead` types the response as `boolean` and discards it.

The adapter's `Notification` interface matches `NotificationDto` field for field.
