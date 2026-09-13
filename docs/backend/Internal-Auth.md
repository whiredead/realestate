# Internal (AuthenticationAPI)

`src/AuthenticationAPI/src/Api/Controllers/InternalController.cs` — class `InternalController`, route prefix **`internal/Internal`**.

Route template is `internal/[controller]`, so the resolved path repeats the
segment. This is not under `api/`.

## Purpose

Service-to-service endpoints called by ProjectAPI: create or activate an account
for a given role, and read the canonical roles an account holds.

ProjectAPI has a controller of the same name and route going the other way — see
`docs/backend/Internal.md`.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| POST | `/internal/Internal/users/provision` | `ProvisionInternalUserHandler` | `{ UserId, Email, WasExistingAccount, RolesAfter }` |
| GET | `/internal/Internal/users/{userId}/roles` | `GetUserRolesHandler` | `{ Exists, UserId, Email, Roles }` |

## Authorization

```csharp
[Authorize(AuthenticationSchemes = InternalApiKeyDefaults.AuthenticationScheme)]
```

A separate scheme from the JWT bearer used by `UserController`. The controller's
doc comment states that schemes are per-action rather than merged, so a valid
user JWT cannot reach these actions and the internal key cannot reach any other.

`InternalApiKeyAuthenticationHandler` reads the `X-Internal-Api-Key` header,
compares it to the configured key with a length check followed by
`CryptographicOperations.FixedTimeEquals`, and on success builds a
`ClaimsIdentity` for the scheme carrying no claims — no user id, no roles.

## Provisioning

`ProvisionInternalUserHandler` is the only place besides `RegisterHandler` that
calls `UserManager.CreateAsync`.

1. `RoleCodes.Normalize(request.RoleCode)`.
2. `FindByEmailAsync(request.Email)`. If an account exists it is **reused**: the
   password is not touched, and only the role is added. `WasExistingAccount` is
   returned as `true`.
3. Otherwise creates a `User` with `Id = Guid.NewGuid().ToString()`,
   `UserName = Email`, `FirstNameAr`/`LastNameAr` empty,
   `Discriminator = roleCode`, using `request.Password`. Identity failures become
   the application's `ValidationException`.
4. Creates the role through `RoleManager` if missing, then `AddToRoleAsync` when
   the normalised current roles do not already contain it.
5. Returns `RolesAfter` as the normalised role set.

The id is generated **here** and returned to the caller, which is the reverse of
ProjectAPI's `ProvisionUserHandler`, where the caller supplies the id.

## Role lookup

`GetUserRolesHandler` returns `Exists = false` with an empty role array for an
unknown id, and otherwise `RoleCodes.Normalize(await _userManager.GetRolesAsync(user))`
alongside the id and email. It never throws for a missing user.

## Dependencies

**Services:** `UserManager<User>`, `RoleManager<Role>`, `InternalApiSettings`.

**Tables written:** `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`
(AuthenticationAPI's own database).

## Relations

**Depended on by**

| Caller | Endpoint | Purpose |
|---|---|---|
| ProjectAPI `AcceptInvitationHandler` | `POST users/provision` | Creates the `BUYER` account when an invited buyer sets their password, via `AuthenticationApiClient.ProvisionBuyerAsync` with `RoleCode = "BUYER"`. See `docs/backend/Invitations.md`. |

**No traced caller** for `GET users/{userId}/roles`. A search across ProjectAPI's
`Infrastructure/Clients/` and `Api/` finds no request to that path, and
`IAuthenticationApiClient` exposes only `ProvisionBuyerAsync`. The controller's
comment describes it as the source of truth for ProjectAPI's membership-grant
role checks, but `CreateProjectMembershipHandler` and
`CreateProjectAssignmentHandler` call their **local** `UserManager.GetRolesAsync`
against ProjectAPI's mirrored tables instead.

**Depends on** — nothing.

## Known edge cases

- `GET users/{userId}/roles` has no caller in either service.
- The scheme authenticates the calling service, not a user, so `ICurrentUser`
  would resolve to nothing for any handler reached this way. Neither handler here
  uses it.
- A single static shared key authenticates the channel, with no per-call
  signature, nonce or expiry. The comparison is constant-time.
- Provisioning an existing account never verifies that the caller knows the
  existing password — it adds the requested role to whatever account matches the
  email. For the traced caller this is the invitation flow, where the token is
  the proof.
- The two services' provisioning endpoints differ in id ownership: this one
  generates the id, ProjectAPI's accepts it from the caller. The invitation flow
  relies on that direction — the id generated here is written back onto
  `CrmContact.UserId` and onto matching reservations by
  `AcceptInvitationHandler`.
- Role membership is written to this database only. An account created here is
  not mirrored into ProjectAPI by this handler, so ProjectAPI's own checks
  against its local `AspNetUserRoles` will not see it until something calls
  ProjectAPI's `Internal` endpoint.

## Frontend coverage

None, by design. These routes use a separate authentication scheme and are
called only by ProjectAPI.
