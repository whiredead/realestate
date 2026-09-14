# Feedback

`src/ProjectAPI/src/Api/Controllers/FeedbackController.cs` — class `FeedbackController`, route prefix **`api/Feedback`**.

## Purpose

User-submitted ratings and comments, optionally attached to a project, with
moderation reads for staff.

## Endpoints

| Verb | Path | Auth | Handler | Response |
|---|---|---|---|---|
| POST | `/api/Feedback/submit-feedback` | `[AllowAnonymous]` | `SubmitFeedbackHandler` | `string` |
| GET | `/api/Feedback/{feedbackId}` | authenticated + inline role check | `GetFeedbackByIdHandler` | feedback details |
| GET | `/api/Feedback/feedbacks` | `AdminsAgents` | `GetFeedbackHandler` | paginated list |

Class-level `[Authorize(AuthenticationSchemes = "Bearer")]`.

## Authorization detail

`GET /{feedbackId}` has no `[Authorize(Roles=…)]` attribute. It performs its own
check in the controller body:

```csharp
var rolesClaim = User.FindFirst("Roles")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
if (rolesClaim != null) {
    var roles = rolesClaim.Split(',').Select(r => r.Trim());
    if (!roles.Contains("Admin") && !roles.Contains("Agent") && !roles.Contains("Acheteur")) return Forbid();
} else return Forbid();
```

This compares against the **legacy** French role labels — `"Admin"`, `"Agent"`,
`"Acheteur"` — not the spec codes `GLOBAL_ADMIN`, `SALES_AGENT`, `BUYER`. It
reads a custom `"Roles"` claim first and falls back to the first `ClaimTypes.Role`
claim.

`TokenProvider` emits both forms: a comma-joined `"Roles"` claim built from
`user.UserRoles` (legacy names), and separate `ClaimTypes.Role` claims carrying
both the legacy label and the normalised spec code. The fallback branch reads
`FindFirst(ClaimTypes.Role)` — a single claim, then splits it on commas, so with
multi-claim role emission it sees one role only.

`POST submit-feedback` is `[AllowAnonymous]`, and a commented-out
`[CustomAuthorize("Acheteur")]` sits above it.

## Submission

`SubmitFeedbackHandler` wraps its whole body in `try/catch (Exception ex)` and
returns `ex.ToString()` as the success-typed `string` result:

```csharp
catch (Exception ex) { return ex.ToString(); }
```

The action returns `Ok(result)` unconditionally, so a failure — including the
`NotFoundException` for an unknown user or project raised inside the same try —
is returned as **HTTP 200 with the full exception text and stack trace in the
body**.

On the success path the handler:

1. Resolves `request.UserId` through `UserManager`; throws `NotFoundException` if
   absent (caught by the block above).
2. When `ProjectId` is supplied, loads it through `_projectRepository`, which is
   an `IImmeubleRepository` — the local variable is typed `Immeuble`. So the id
   is validated against the **Immeubles** table, not `Projects`.
3. Inserts a `Feedback` with `CreatedAt = DateTime.Now` (local time).

`SubmitFeedbackValidator` requires `UserId`, a `Rating` between 1 and 5, and
`Comments` of at most 1000 characters. Since the endpoint is anonymous, `UserId`
is supplied entirely by the caller.

## Listing

`GetFeedbackHandler` accepts `UserId`, `ProjectId`, `AgentId`, `PageNumber`,
`PageSize`. When `AgentId` is supplied it resolves that agent's active
`SALES_AGENT` `ProjectMembership` project ids and restricts results to feedback
on those projects. With no `AgentId` the query is unrestricted — there is no
perimeter check based on the **caller**, only on the optionally-supplied
`AgentId` filter.

Results are loaded in full via `Find(predicate, includes)` and paginated in
memory.

## Dependencies

**Services:** `IFeedbackRepository`, `IImmeubleRepository`, `UserManager<User>`,
`ApplicationDbContext`.

**Tables written:** `Feedbacks`.
**Tables read:** `Feedbacks`, `AspNetUsers`, `Immeubles`, `ProjectMemberships`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| ProjectMembership | The `AgentId` filter resolves active `SALES_AGENT` memberships. |
| Immeuble | `ProjectId` on the command is validated against `Immeubles`. |

**Depended on by** — nothing traced.

`Communication` and `Incident` entities live alongside `Feedback` in
`Domain/FeedBacks/Entities/`; no handler in this module reads or writes them.

## Known edge cases

- `POST submit-feedback` returns **200 with an exception stack trace** whenever
  the handler throws, because the catch converts the exception into the success
  return value.
- The endpoint is anonymous but `UserId` is required and caller-supplied, so
  anyone can submit feedback attributed to any existing user id.
- `Feedback.ProjectId` is validated against `Immeubles`, while the field name and
  the `AgentId` filter both treat it as a project id — the filter compares
  `f.ProjectId` against `ProjectMembership.ProjectId`.
- `GET /{feedbackId}` gates on legacy role labels only, so an account holding
  only spec codes fails the check; and its fallback path reads a single
  `ClaimTypes.Role` claim then splits it on commas.
- The list endpoint applies no caller-based perimeter; any `AdminsAgents` caller
  may read all feedback for all projects unless they voluntarily pass an
  `AgentId`.
- `CreatedAt` uses `DateTime.Now`.
- Pagination is applied in memory after loading all matching rows.

## Frontend coverage

`realestateFront/src/api/http/feedbackApi.ts`; UI in
`features/feedback/FeedbackPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| GET `feedbacks` | `feedbackApi.list` |
| GET `{feedbackId}` | `feedbackApi.getById` |
| POST `submit-feedback` | `feedbackApi.submit` |

All three endpoints are called. `feedbackApi.submit` types the response as
`string` and discards it, so the exception-as-200 case is indistinguishable from
success on the client.
