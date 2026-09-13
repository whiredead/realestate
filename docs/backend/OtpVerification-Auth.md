# OTP Verification (AuthenticationAPI)

`src/AuthenticationAPI/src/Api/Controllers/OtpVerificationController.cs` — class `OtpVerificationController`, route prefix **`anonym-users`**.

The route is not under `api/`.

## Purpose

A phone-number verification flow: request a six-digit code, then exchange it for
a token. Both steps run before any session exists.

## Endpoints

| Verb | Path | Handler | Request | Response |
|---|---|---|---|---|
| POST | `/anonym-users/request-code` | `DemandeCodeHandler` | `DemandeCodeCommand` (phone number) | 204 No Content |
| POST | `/anonym-users/login` | `VerifyCodeHandler` | `VerifyCodeCommand` (code) | `string` token |

Class-level `[AllowAnonymous]`.

## Storage

`IOtpVerificationRepository` is implemented by `OtpVerificationRepository`, which
is backed by a **`ConcurrentDictionary<string, OtpVerification>` held in
process memory**:

```csharp
private readonly ConcurrentDictionary<string, OtpVerification> _items = new(StringComparer.Ordinal);
```

No database table, no cache server. Codes do not survive a restart and are not
shared across instances. `appsettings.json` carries a `DatabaseConfig` section
naming a `verification-otp` container, and the repository's doc comments mention
partition keys, but the implementation ignores both.

## Request-code

`DemandeCodeHandler`:

1. Reads `ExpiredCode` and `AllowedAttemps` from `OtpSettings`.
2. Loads prior requests, takes the newest by `ExpiredOn`.
3. Throws `TooManyAttemptsException` when the last request is still unexpired
   **or** `Attempts >= AllowedAttemps`.
4. Generates a code with `random.Next(100000, 1000000)` formatted `D6`.
5. Stores a new `OtpVerification` with `Attempts = lastRequest.Attempts + 1`
   (or 1), and
   `ExpiredOn = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss")) + ExpiredCode`.

Timestamps are encoded as `yyyyMMddHHmmss` parsed into a `long`, and expiry is
computed by **adding `ExpiredCode` to that number**. Because the encoding is
positional rather than a duration, the addition does not represent a fixed time
span: with the configured `ExpiredCode = 500`, adding 500 to
`20260912183000` yields `20260912183500`, but adding 500 to a value ending in
`...5900` produces `...6400`, which is not a valid timestamp encoding.

`GenerateRandomCodeString` uses `System.Random`, not a cryptographic generator.

**No code is ever sent.** There is no SMS client, email call or other delivery in
this module — a search across `Application/AuthenticationOtp/` finds no sender.
The handler returns 204 and the generated code exists only in the in-memory
dictionary. *Statically verified, runtime untested:* this establishes that no
in-process code path delivers the code; it does not establish that no external
process reads it, though the in-memory store makes that impractical.

## Verify

`VerifyCodeHandler`:

1. Looks up a verification by **`r.Code == request.Code` alone** — the phone
   number is not part of the lookup.
2. Absent ⇒ `NotFoundException`.
3. `ExpiredOn < now` ⇒ `InvalidOperationException`.
4. Sets `IsAuthenticated = true`, updates the item, and returns
   `ITokenProvider.GenerateOtpToken(updatedAnonymUser)`.

`GenerateOtpToken` issues a JWT containing a single `phoneNumber` claim, signed
with `OtpSettings.SecretKey` and using `OtpSettings.Issuer` /
`OtpSettings.Audience` — **different key, issuer and audience** from the access
token issued by `LoginHandler`.

`Program.cs` configures JWT bearer validation against `JwtSettings` only, so a
token produced here does not authenticate against any `[Authorize]` endpoint in
either service.

## Dependencies

**Services:** `IOtpVerificationRepository` (in-memory), `ITokenProvider`,
`OtpSettings`.

**Tables:** none.

## Relations

**Depends on** — nothing.

**Depended on by** — nothing traced. No handler, controller or client in either
service consumes an OTP token, and `Program.cs` registers no authentication
scheme that would validate one.

## Known edge cases

- The code store is in-process memory, so codes are lost on restart and are not
  visible to a second instance.
- No delivery mechanism exists, so a caller cannot learn the code through the
  application.
- `VerifyCodeHandler` matches on the code alone. With a six-digit space and no
  phone-number binding, any outstanding code verifies for whoever submits it.
- There is no attempt limit on **verification** — the counter in
  `DemandeCodeHandler` limits requests, not guesses.
- `Attempts` is incremented on each request and never reset, so once it reaches
  `AllowedAttemps` the phone number can never request another code for the
  lifetime of the process.
- Expiry arithmetic adds a scalar to a `yyyyMMddHHmmss`-encoded number rather
  than to a timestamp.
- `System.Random` is used to generate the code.
- The OTP token is signed with a different key/issuer/audience than the access
  token and is not validated by any configured authentication scheme.
- `InvalidOperationException` for an expired code is not registered in the
  exception filter, so it surfaces as a 500; the controller declares
  `[ProducesResponseType(500)]` on that action.

## Frontend coverage

**No frontend caller.** A search for `anonym-users` across
`realestateFront/src/` returns no matches. Neither endpoint is reachable from
this UI.
