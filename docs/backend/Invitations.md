# Invitations

`src/ProjectAPI/src/Api/Controllers/InvitationsController.cs` — class `InvitationsController`, route prefix **`api/invitations`**.

## Purpose

Accepts an `AccountInvitation` token and turns it into a real login. The
invitation is issued elsewhere — `ApproveReservationHandler` calls
`IAccountInvitationService.IssueAsync` after approving a reservation whose buyer
has no account (see `docs/backend/Reservations.md`).

ProjectAPI owns the invitation and the `CrmContact`; AuthenticationAPI owns the
account. This handler bridges the two.

## Endpoints

| Verb | Path | Handler | Request | Response |
|---|---|---|---|---|
| POST | `/api/invitations/accept` | `AcceptInvitationHandler` | `{ Token, Password }` | `{ UserId, Email, Message }` |

## Authorization

Class-level `[AllowAnonymous]`. The invitee has no session; the token is the only
credential. The controller's doc comment compares the posture to
AuthenticationAPI's confirm-email and reset-password endpoints.

## Invitation issuance (upstream)

`AccountInvitationService.IssueAsync(crmContactId)`:

- returns `null` when the contact has no email;
- sets `RevokedAt = now` on every prior invitation for that contact where
  `AcceptedAt == null && RevokedAt == null`, so a contact holds at most one live
  token;
- inserts an `AccountInvitation` with
  `Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))` (32 random
  bytes, hex-encoded), `Email` copied from the contact, and
  `ExpiresAt = now + 7 days` (`Validity`).

## Acceptance

`AcceptInvitationHandler`:

1. Blank token ⇒ `BusinessRuleException(ValidationFailed)`.
2. Loads the `AccountInvitation` by token including its `CrmContact`; absent ⇒
   `NotFoundException`.
3. Three terminal-state refusals, each with its own code, all **409**:

   | Condition | Code |
   |---|---|
   | `AcceptedAt != null` | `INVITATION_ALREADY_ACCEPTED` |
   | `RevokedAt != null` | `INVITATION_REVOKED` |
   | `ExpiresAt <= UtcNow` | `INVITATION_EXPIRED` |

4. Calls `IAuthenticationApiClient.ProvisionBuyerAsync(email, firstName,
   lastName, password, phone)`.
5. If `contact.UserId` is already set and differs from the provisioned id ⇒
   `BusinessRuleException(ValidationFailed)` — "Ce contact est déjà lié à un
   autre compte."
6. Writes `contact.UserId`, promotes `LifecycleStatus` `Prospect → Buyer`, sets
   `UpdatedAt`.
7. Sets `invitation.AcceptedAt = UtcNow`.
8. **Back-links reservations**: every `Reservation` where
   `PrimaryContactId == contact.Id && string.IsNullOrEmpty(BuyerId)` gets
   `BuyerId = provisioned.UserId`.
9. One `SaveChangesAsync`.

Step 8 is what makes previously account-less files visible in the buyer portal,
since those reads filter on `Reservation.BuyerId`.

## Cross-service call

`AuthenticationApiClient.ProvisionBuyerAsync` posts:

```
POST {InternalApi:AuthApiBaseUrl}/internal/Internal/users/provision
X-Internal-Api-Key: <configured key>
{ Email, FirstName, LastName, Password, RoleCode = "BUYER", PhoneNumber }
```

A non-success status is logged and rethrown as `InvalidOperationException`, so
the invitation is **not** marked accepted and no `contact.UserId` link is written
when provisioning fails. The client's own comment contrasts this with
`ProjectApiClient.ProvisionUserAsync`, which is fire-and-forget.

On the AuthenticationAPI side, `ProvisionInternalUserHandler` reuses an existing
account matched by email without touching its password, and only adds the role —
see `docs/backend/Internal-Auth.md`.

## Dependencies

**Services:** `ApplicationDbContext`, `IAuthenticationApiClient`,
`InternalApiSettings` (base URL and key), and upstream
`IAccountInvitationService`.

**Tables written:** `AccountInvitations`, `CrmContacts`, `Reservations`
(`BuyerId` back-link).
**Tables read:** `AccountInvitations`, `CrmContacts`, `Reservations`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| Reservations | `ApproveReservationHandler` issues the invitation; acceptance back-links reservations by `PrimaryContactId`. |
| AuthenticationAPI | `POST /internal/Internal/users/provision` over the internal API key scheme. |

**Depended on by** — nothing in ProjectAPI calls this controller.

## Known edge cases

- **No in-process code path delivers the invitation.** `IEmailService` has no
  caller anywhere in ProjectAPI, and `ApproveReservationHandler` performs no send
  after `IssueAsync` — its own comment states that sending is outside the
  handler's concern and that no email provider is wired in. *Statically verified,
  runtime untested:* this establishes that no ProjectAPI code sends the link. It
  does not establish that no external or operational process does — a separate
  job, a database trigger, or a manual runbook could read `AccountInvitations`
  and deliver it. Not tested at runtime.
- A contact with no email gets no invitation: `IssueAsync` returns `null` and
  `ApproveReservationHandler` logs it. Nothing surfaces in the UI.
- The endpoint is anonymous and, in the traced path, unthrottled — there is no
  rate limit or lockout on token attempts. The 64-hex-character token space is
  the only protection.
- Password rules are enforced by AuthenticationAPI's Identity options;
  `AcceptInvitationCommand` has no `AbstractValidator` in ProjectAPI, so a weak
  password surfaces as a 422 relayed from the other service.
- Acceptance does not sign the user in. No token is issued by this flow; the
  response carries `UserId`, `Email` and a message.

## Frontend coverage

`realestateFront/src/api/http/invitationsApi.ts`; UI in
`features/auth/ActivateAccountPage.tsx`, route `/activate?token=…`.

| Endpoint | Frontend caller |
|---|---|
| POST `accept` | `invitationsApi.accept` |

The page reads `token` from the query string and renders an invalid-link state
without calling the API when it is absent. After a successful response it shows
the activated email and attempts a login with that email and the submitted
password; per `realestateFront/docs/frontend/Auth.md`, a login failure at that
point is deliberately non-fatal and the user is directed to `/login`.
