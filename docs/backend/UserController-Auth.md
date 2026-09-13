# User (AuthenticationAPI)

`src/AuthenticationAPI/src/Api/Controllers/UserController.cs` — class `UserController`, route prefix **`api/User`**.

This is the only public-facing controller in AuthenticationAPI. It is the source
of truth for accounts, roles and passwords; ProjectAPI keeps a mirrored
`AspNetUsers` table for its own foreign keys.

## Purpose

Login, public registration, admin account creation, email confirmation, password
reset, lockout, and account listing/editing/deletion.

## Endpoints

| Verb | Path | Auth | Handler |
|---|---|---|---|
| POST | `/api/User/login` | **Anonymous** | `LoginHandler` |
| POST | `/api/User` | **Anonymous** | `RegisterHandler` |
| POST | `/api/User/admin-create` | `GLOBAL_ADMIN` | `CreateUserByAdminHandler` |
| POST | `/api/User/confirm-email` | **Anonymous** | `ConfirmEmailHandler` |
| GET | `/api/User` | `GLOBAL_ADMIN` | `GetAllUsersHandler` |
| GET | `/api/User/by-role/{role}` | authenticated | `GetUsersByRoleHandler` |
| GET | `/api/User/multiple` | authenticated | `GetUsersByIdsHandler` |
| GET | `/api/User/roles` | authenticated | `GetAllRolesHandler` |
| GET | `/api/User/lockout/{id}` | `GLOBAL_ADMIN` | `LockoutUserHandler` |
| GET | `/api/User/unlock/{id}` | `GLOBAL_ADMIN` | `UnlockUserHandler` |
| POST | `/api/User/forgot-password` | **Anonymous** | `ForgotPasswordHandler` |
| POST | `/api/User/reset-password` | **Anonymous** | `ResetPasswordHandler` |
| POST | `/api/User/admin-change-password` | `GLOBAL_ADMIN` | `AdminChangePasswordHandler` |
| DELETE | `/api/User/{id}` | `GLOBAL_ADMIN` | `DeleteUserHandler` |
| PUT | `/api/User/{id}` | `GLOBAL_ADMIN` | `UpdateUserHandler` |

Class-level `[Authorize]`; five actions opt out with `[AllowAnonymous]`.

The controller declares its own admin constant:

```csharp
private const string Admins = RoleCodes.GlobalAdmin;
```

So every `[Authorize(Roles = Admins)]` here means **`GLOBAL_ADMIN` only** — a
`PROJECT_ADMIN` cannot manage account records. This differs from ProjectAPI's
`RoleGroups.Admins`, which includes both.

Lockout and unlock are **GET** requests that mutate state.

## Login

`LoginHandler` looks the user up by name, calls
`_signInManager.PasswordSignInAsync(user, password, false, lockoutOnFailure: false)`,
and on success issues a JWT via `ITokenProvider` and returns:

```
{ User, AccessToken, IsAutheticated, Message, Roles }
```

`Roles` is `RoleCodes.Normalize(user.GetRoleNames())` — **spec codes only**,
while the JWT itself carries both vocabularies (see below).

`lockoutOnFailure: false` means failed attempts do not increment the Identity
lockout counter, so the lockout/unlock endpoints are the only path to a locked
account.

The whole body is wrapped in `try/catch (Exception)`, and both the
wrong-password path and the exception path return the same
`IsAutheticated = false`, `"Invalid username or password"` response with a 200
status. The controller adds its own `try/catch` returning `BadRequest(ex.Message)`,
which the handler's own catch makes unreachable.

## Token contents

`TokenProvider.GenerateAccessToken` emits:

| Claim | Value |
|---|---|
| `userName`, `firstName`, `lastName`, `userId` | profile fields |
| `Roles` | a **single comma-joined string** built from `user.UserRoles.Select(r => r.Role.Name)` — stored/legacy names |
| `ClaimTypes.Role` (repeated) | for each role, both the stored label **and** `RoleCodes.Normalize(label)` |

The two role representations come from different expressions —
`user.UserRoles` for the `"Roles"` claim and `user.GetRoleNames()` for the
`ClaimTypes.Role` claims.

`[Authorize(Roles = …)]` and `ICurrentUser` read `ClaimTypes.Role`. Controllers
that read the custom `"Roles"` claim see legacy labels only — see
`docs/backend/Appointments.md` and `docs/backend/Feedback.md`.

Expiry is `JwtSettings.ExpirationTokenInMinutes`, a computed property equal to
`ExpirationTokenInHours * 60`; the configured value is 48 hours.

## Registration

`RegisterValidator` requires username, first/last name, email, a password with an
uppercase letter and an alphanumeric character, and a phone matching
`^(06|07)\d{8}$`. `Roles` must be non-null and every entry must be in
`AllowedRoles`, which lists the spec codes plus legacy labels — and excludes
`VISITOR` and `BUYER`.

`RegisterHandler` then applies its own guard, independent of the validator:

- any requested role in `RoleCodes.Internal` ⇒ `ValidationException`
  ("Les comptes internes sont créés uniquement par invitation");
- a requested `BUYER` ⇒ `ValidationException` ("Le statut Acheteur est attribué
  automatiquement après approbation d'une réservation…").

Whatever passes, the account is created with `Discriminator = PROSPECT` and
`AddToRolesAsync(user, [PROSPECT])`. The requested roles are never persisted.

So `AllowedRoles` permits internal codes that the handler then rejects: the
validator is the wider gate and the handler is the effective one.

The Arabic name columns are NOT NULL in the schema and optional on the command,
so the handler coalesces them to empty strings.

**Registration does not mirror the account into ProjectAPI.** Only
`CreateUserByAdminHandler` calls `IProjectApiClient.ProvisionUserAsync`.

## Admin creation

`CreateUserByAdminHandler`:

1. Rejects a duplicate email with `ValidationException`.
2. Creates the `User` with a caller-supplied password and
   `Discriminator = roleCode`.
3. Creates the role in `RoleManager` if it does not exist, then
   `AddToRoleAsync`.
4. Calls `IProjectApiClient.ProvisionUserAsync(id, email, firstName, lastName,
   phone, roleCode)` to mirror the account into ProjectAPI's `AspNetUsers` —
   see `docs/backend/Internal.md`.

The handler comment records why step 4 matters: ProjectAPI resolves
`Appointment.SalesAgentId`, `ProjectMembership.UserId` and similar FKs against
its own table, so an account existing only here fails ProjectAPI's
"User not found" checks.

## Deletion

`DeleteUserHandler` loads the user (404 if absent), checks for dependencies, and
returns a `DeleteUserResponse` rather than throwing. A `DbUpdateException` from a
foreign-key constraint is caught and converted into a failure response listing
the dependencies. The controller maps a failure response to 400.

## Dependencies

**Services:** `UserManager<User>`, `SignInManager<User>`, `RoleManager<Role>`,
`ITokenProvider`, `IUserRepository`, `ApplicationDbContext`,
`IProjectApiClient`.

**Tables written:** `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`.

## Relations

**Depended on by**

| Consumer | Mechanism |
|---|---|
| ProjectAPI (all of it) | JWTs issued here are validated by ProjectAPI using the shared signing key, issuer and audience. |
| ProjectAPI `Internal` | `CreateUserByAdminHandler` posts to `/internal/Internal/users/provision`. |
| Frontend | `authApi` and `usersApi` both target this controller. |

**Depends on**

| Module | Mechanism |
|---|---|
| ProjectAPI `Internal` | `IProjectApiClient.ProvisionUserAsync` on admin creation. |

`docs/backend/Invitations.md` covers the reverse direction — ProjectAPI calling
AuthenticationAPI's `Internal` controller to create a `BUYER` account.

## Known edge cases

- Lockout and unlock are `GET` requests that change state.
- `PasswordSignInAsync` is called with `lockoutOnFailure: false`, so repeated
  failed logins never lock an account automatically.
- A failed login returns **200** with `IsAutheticated = false`; the handler
  catches all exceptions and returns the same body, so transport-level and
  credential failures are indistinguishable to the caller. The controller's
  `BadRequest(ex.Message)` branch is unreachable because the handler never
  rethrows.
- `RegisterValidator.AllowedRoles` accepts internal role codes that
  `RegisterHandler` then rejects with a different message; and any accepted role
  is discarded in favour of `PROSPECT`.
- Public registration does not mirror the account into ProjectAPI, so a
  self-registered `PROSPECT` has no row there until some other path creates one.
- `PUT /api/User/{id}` carries an inline comment noting there is no self-scope
  check; it is `GLOBAL_ADMIN`-only and edits any account by id.
- `GET /api/User/by-role/{role}`, `GET /api/User/multiple` and
  `GET /api/User/roles` have no role attribute, so any authenticated caller —
  including a `PROSPECT` — can enumerate users by role, fetch users by id, and
  list all roles.
- `DeleteUser` and `UpdateUser` return failures as 400 with a response object
  rather than through the exception filter.

## Frontend coverage

`realestateFront/src/api/http/authApi.ts` and `usersApi.ts`, both using
`authFetch`. UI in `features/auth/*` and `features/users/UsersPage.tsx`.

| Endpoint | Frontend caller |
|---|---|
| POST `login` | `authApi.login` |
| POST `/` (register) | `authApi.register` |
| GET `by-role/{role}` | `authApi.listByRole` |
| GET `/` | `usersApi.list` |
| POST `admin-create` | `usersApi.create` |
| PUT `{id}` | `usersApi.update` |
| DELETE `{id}` | `usersApi.remove` |
| GET `lockout/{id}` | `usersApi.lock` |
| GET `unlock/{id}` | `usersApi.unlock` |
| POST `admin-change-password` | `usersApi.changePassword` |
| POST `confirm-email` | **no caller** |
| POST `forgot-password` | **no caller** |
| POST `reset-password` | **no caller** |
| GET `multiple` | **no caller** |
| GET `roles` | **no caller** |

**10 of 15 endpoints are called.** The entire password-recovery flow
(`forgot-password`, `reset-password`) and `confirm-email` have no frontend
caller, so those journeys are not reachable from this UI.

`authApi.register` types the response as `unknown` and discards it; per its own
comment the endpoint returns only the new account's id, so the adapter follows
the registration with a login call. `authApi.currentUser()` always returns
`null` — see `realestateFront/docs/frontend/_ApiClient.md`.
