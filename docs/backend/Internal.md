# Internal (ProjectAPI)

`src/ProjectAPI/src/Api/Controllers/InternalController.cs` — class `InternalController`, route prefix **`internal/Internal`**.

Route template is `internal/[controller]`, so the resolved path repeats the
segment. This is not under `api/`.

## Purpose

A service-to-service endpoint called by AuthenticationAPI to mirror an account
into ProjectAPI's own `AspNetUsers` table. ProjectAPI validates JWTs issued by
AuthenticationAPI using the shared signing key but keeps its own user rows, which
its handlers query through `UserManager`.

AuthenticationAPI has a controller of the same name and route going the other
way — see `docs/backend/Internal-Auth.md`.

## Endpoints

| Verb | Path | Handler | Response |
|---|---|---|---|
| POST | `/internal/Internal/users/provision` | `ProvisionUserHandler` | `{ Id, AlreadyExisted }` |

## Authorization

```csharp
[Authorize(AuthenticationSchemes = InternalApiKeyDefaults.AuthenticationScheme)]
```

A **separate authentication scheme** from the JWT bearer used everywhere else,
registered in `Program.cs` and implemented by
`InternalApiKeyAuthenticationHandler`:

- reads the header `X-Internal-Api-Key`;
- missing header ⇒ `AuthenticateResult.Fail("Missing internal API key.")`;
- compares against `InternalApiSettings.ApiKey` using
  `CryptographicOperations.FixedTimeEquals` after a length check;
- on success builds an empty `ClaimsIdentity` for the scheme — **no user id, no
  roles**.

Because the principal carries no claims, `ICurrentUser.UserId` is null and
`ICurrentUser.Roles` is empty for any handler reached through this scheme. The
only handler behind it does not use `ICurrentUser`.

`InternalApiSettings.ApiKey` is read from configuration at startup in
`Program.cs`, which throws if it is absent.

## Behaviour

`ProvisionUserHandler`:

1. `UserManager.FindByIdAsync(request.Id)`. If the row exists it calls
   `EnsureRoleAsync` and returns `AlreadyExisted = true` — idempotent, and it
   repairs a prior call that created the row but failed before granting the role.
2. Otherwise it instantiates the CLR type matching the role, because the model
   uses table-per-hierarchy and the discriminator is fixed at creation:

   | `request.Role` | Type created |
   |---|---|
   | `SALES_AGENT` | `Agent` |
   | `NOTARY` | `Notary` |
   | anything else | `User` |

3. Copies `Id`, `UserName = Email`, `Email`, `FirstName`, `LastName`,
   `PhoneNumber`, and sets `EmailConfirmed = true`.
4. Creates the account with a generated throwaway password
   (`Guid.NewGuid().ToString("N") + "Aa1!"`). The handler's comment states this
   service never authenticates logins directly, so the password exists only
   because Identity requires one. Identity failures become the application's
   `ValidationException`.
5. `EnsureRoleAsync` grants the role row in **this** database's
   `AspNetUserRoles`, because handlers here check `UserManager.GetRolesAsync`
   rather than the TPH discriminator — `CreateProjectMembershipHandler` is the
   example named in the code.

The **id is supplied by the caller** and reused verbatim, so the same user id
identifies the account in both services.

## Dependencies

**Services:** `UserManager<User>`, `RoleManager<IdentityRole>`,
`InternalApiSettings`.

**Tables written:** `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` (ProjectAPI's
own database).

## Relations

**Depended on by** — AuthenticationAPI. `ProjectApiClient.ProvisionUserAsync` on
that side posts here; per the comment in ProjectAPI's own
`AuthenticationApiClient`, that call is fire-and-forget, mirroring an account
that already exists elsewhere.

**Depended on by, indirectly** — every ProjectAPI handler that resolves a user
through `UserManager`: `CreateReservationHandler` (agent validation),
`AssignNotaireToReservationHandler`, `CreateProjectMembershipHandler` and
`CreateProjectAssignmentHandler` (role-match guard),
`CreateAppointmentHandler`. Those checks only pass for accounts that have been
mirrored here.

**Depends on** — nothing in ProjectAPI.

## Known edge cases

- The scheme authenticates the **caller service**, not a user. Any handler
  reached this way sees a null `ICurrentUser.UserId`, so a handler that assumed a
  user identity would silently get none.
- A single static shared key authenticates the whole channel; there is no per-call
  signature, nonce or expiry. The comparison itself is constant-time.
- The route is `internal/Internal` rather than `internal/users` or similar,
  because the controller class is named `InternalController` and the template uses
  `[controller]`.
- An account created here has a password that is generated and discarded, so it
  can never be used to sign in against ProjectAPI directly.
- `EnsureRoleAsync` grants the role but the TPH discriminator is fixed at
  creation: a user mirrored as a base `User` cannot later become an `Agent` or
  `Notary` through this endpoint, since step 1 returns early for an existing row.

## Frontend coverage

None, by design. This route is not reachable from the browser — it uses a
separate authentication scheme and is called only by AuthenticationAPI.
