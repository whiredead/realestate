# Wiring the React frontend to GPIA_RealEstate backend

This document is for whoever (Claude Code or a developer) implements the
`src/api/http/*` adapters in `gpia-realestate-frontend` and switches
`src/api/index.ts` from the mock adapters to real HTTP calls. It's a
complete map of the backend's actual endpoints, request/response shapes,
auth behavior, and every place the current mock data disagrees with the
real API — so nothing has to be re-discovered by reading C# source again.

Backend repo: `GPIA_RealEstate-master` (two independent ASP.NET Core 8
Web APIs under `src/AuthenticationAPI` and `src/ProjectAPI`).

Frontend repo: `gpia-realestate-frontend` (React + TS + Vite), delivered
previously. Its architecture already anticipates this: every feature page
calls `src/api/index.ts`, which re-exports the mock adapters from
`src/api/mock/mockApi.ts`. To go live, implement the same interfaces
(`src/api/interfaces.ts`) under `src/api/http/*.ts` and flip the exports in
`src/api/index.ts`. No page/component code should need to change.

---

## 1. Before anything else: fix the port collision

Both APIs' `Properties/launchSettings.json` default the `Project` profile to
**the same port, 48988**:

- `src/AuthenticationAPI/src/Api/Properties/launchSettings.json` → `http://localhost:48988/`
- `src/ProjectAPI/src/Api/Properties/launchSettings.json` → `http://localhost:48988/`

They cannot both run on 48988 at once. Change one of them (recommended:
leave AuthenticationAPI on `48988`, move ProjectAPI to `48989`) before
starting both services, e.g. edit ProjectAPI's `applicationUrl` /
`launchUrl` to `http://localhost:48989/`, or run it with:

```bash
dotnet run --project src/ProjectAPI/src/Api --urls http://localhost:48989
```

Both APIs already have Swagger UI enabled in Development
(`/swagger`) and a health check at `/health` — use those to sanity-check
each service is up before wiring the frontend.

## 2. Base URLs and environment variables

The frontend already reads these in `src/lib/config.ts`:

```ts
authApiUrl: import.meta.env.VITE_AUTH_API_URL ?? "https://localhost:5001",
projectApiUrl: import.meta.env.VITE_PROJECT_API_URL ?? "https://localhost:5002",
```

Create a `.env` (or `.env.local`) in the frontend root:

```
VITE_AUTH_API_URL=http://localhost:48988
VITE_PROJECT_API_URL=http://localhost:48989
```

Both APIs enable `AllowAll` CORS (`AllowAnyOrigin/Method/Header`) in
`Program.cs`, so calling them from `http://localhost:5173` (Vite dev
server) works with no extra config.

## 3. Authentication — how it actually works

- **AuthenticationAPI** (`UserController`, `OtpVerificationController`) is
  where users log in / register. `POST /api/User/login` returns a JWT
  (`LoginResponse.AccessToken`) signed with the settings in
  `appsettings.json` → `JwtSettings` (`Issuer: your_issuer`,
  `Audience: your_audience`, a shared `SecretKey`).
- **ProjectAPI** validates that *same* JWT — it reads the identical
  `JwtSettings` block from its own `appsettings.json` (same Issuer /
  Audience / SecretKey values are already duplicated in both projects).
  So: log in against AuthenticationAPI, then send that token as
  `Authorization: Bearer <token>` to ProjectAPI.
- **Important — most ProjectAPI endpoints do not currently enforce auth.**
  Class-level `[Authorize]` is present but commented out on
  `ProjectController`, `ImmeubleController`; `NotaryAppointmentsController`
  has `[AllowAnonymous]` at class level. The only endpoints that actually
  require a valid Bearer token + specific roles today are:
  - `AppointmentsController`: class has `[Authorize(AuthenticationSchemes = "Bearer")]`,
    but almost every action is individually marked `[AllowAnonymous]`
    **except** `GET /api/appointments/user/{userId}` and
    `GET /api/appointments/project/{projectId}`, which require
    `[Authorize(Roles = "Acheteur,Admin,Agent")]`.
  - `FeedbackController`: same pattern — class-level Bearer auth, but
    every current action is `[AllowAnonymous]`.
  - Every other controller (Project, Immeuble, Reservations, Sales,
    Claims, Deliveries, TypeBiens, ProjectAssignment, NotaryBlocks,
    AdminDashboard, File) has **no active `[Authorize]`** — they're
    open right now. Treat this as a known gap, not a spec: if the backend
    team locks these down later, the frontend already sends the Bearer
    token on every request (see §6), so nothing will break — just start
    getting 401s where none occurred before.
- **UserController itself has no `[Authorize]` at all** — `GET /api/User`
  (list users), lockout/unlock, delete, etc. are all unauthenticated today.

**Recommended http client behavior:** always attach
`Authorization: Bearer <token>` (from `AuthContext`) when a token exists,
regardless of whether the specific endpoint currently checks it — that way
the frontend won't need changes when the backend tightens auth.

### OTP login alternative

There's a second, phone-based login flow the current mock doesn't model:

- `POST {authApi}/anonym-users/request-code` — body `{ "phoneNumber": string }`, returns `204 No Content`, sends an SMS/code.
- `POST {authApi}/anonym-users/login` — body `{ "code": string }`, returns a JWT string on success.

Not required for the admin/CRM flows already built, but worth knowing it
exists if a "login with phone" feature comes up.

## 4. Serialization gotchas (read this before writing the http adapters)

Neither API configures `JsonStringEnumConverter` or a custom naming
policy. That means:

1. **Property casing**: ASP.NET Core's default `System.Text.Json` uses
   **camelCase** — so `ProjectResponse.Name` → `"name"` in the JSON. This
   already matches the frontend's TS types (also camelCase), so no mapping
   needed there.
2. **Enums serialize as integers, not strings.** This is the one real trap.
   Several response/request DTOs use C# enums; unless you add a
   `JsonStringEnumConverter` on the backend (recommended — see §8), the
   wire format is a number. The frontend's mock types use readable string
   literals (`"Pending"`, `"Approved"`, ...), so the http adapters must
   translate both directions. Enum tables:

   **`ReservationStatus`** (`GetReservationsResponse.Status`, `GetReservationByIdResponse.Status`)
   | int | string |
   |---|---|
   | 0 | Pending |
   | 1 | Approved |
   | 2 | Rejected |
   | 3 | Cancelled |
   | 4 | Sold |

   **`ClaimStatus`** (`AfterSaleClaimResponse.Status`, `UpdateClaimStatusCommand.NewStatus`)
   | int | string |
   |---|---|
   | 0 | New |
   | 1 | Read |
   | 2 | InProgress |
   | 3 | Resolved |
   | 4 | Rejected |

   Note: the frontend mock's `ClaimStatus` type currently only has
   `Open / InProgress / Resolved / Rejected` — there's no `New`/`Read`
   distinction in the mock. Add those two when wiring for real, or map
   `New → "Open"` if you want to keep the UI's simpler 4-state model (but
   then `updateStatus` must send `0` for "New", not a value the UI can produce).

   **`ClaimCategory`** (`AfterSaleClaimResponse.Category`, `CreateAfterSaleClaimCommand.Category`)
   | int | string |
   |---|---|
   | 0 | General |
   | 1 | Electricity |
   | 2 | Plumbing |
   | 3 | Finishing |
   | 4 | DoorsWindows |
   | 99 | Other |

   The frontend mock's categories (`Plumbing/Electrical/Structural/Finishing/Other`)
   don't line up 1:1 — `Electrical→Electricity`, `Structural` doesn't exist
   on the backend, `DoorsWindows` doesn't exist in the mock. Reconcile the
   `ClaimCategory` union in `src/types/index.ts` to match the real 6 values
   before wiring.

   **`ClaimPriority`** (`AfterSaleClaimResponse.Priority`)
   | int | string |
   |---|---|
   | 0 | Low |
   | 1 | Normal |
   | 2 | High |
   | 3 | Critical |

   Frontend mock uses `Low/Medium/High/Urgent` — rename `Medium→Normal`,
   `Urgent→Critical` to match.

   **`ProjectStatus`** (`ImmeubleResponse.Status`, `UpdateImmeubleCommand.Status`)
   | int | string |
   |---|---|
   | 0 | ComingSoon |
   | 1 | UnderConstruction |
   | 2 | Available |
   | 3 | Sold |

   This one already matches the frontend's `ProjectStatus` type string
   values — only the wire format (int vs string) needs an adapter.

   Everything else that looks like a status (`Appointment.Status`,
   `Delivery.Status`, `NotaryAppointment.Status`,
   `ReservationDocument`, `CreateNotaryAppointmentCommand.Status`) is a
   **plain `string` property in the backend**, not a C# enum — so those
   already round-trip as strings with no int/string translation needed.
   Their allowed values aren't enforced by a backend enum, so keep using
   the frontend's existing string sets (`EnCoursDeTraitement`, `Confirmed`,
   `Scheduled`, etc.) but verify against the relevant command handler if a
   value gets silently rejected.

3. **`decimal` → JSON number.** No special handling needed, just don't
   assume string.

## 5. Response envelope: `PaginatedResponse<T>`

Every "list" endpoint returns:

```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "totalItems": 42,
  "totalPages": 5,
  "data": [ ... ]
}
```

This already matches `src/types/index.ts`'s `PaginatedResponse<T>` — no
mapping needed, just plug the real fetch response straight into the
existing type.

## 6. Suggested http client shape

```ts
// src/api/http/client.ts
import { config } from "../../lib/config";

async function request<T>(base: string, path: string, init: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem("gpia_token"); // or read from AuthContext/a ref
  const res = await fetch(`${base}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${res.status} ${res.statusText}: ${text}`);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

export const authFetch = <T>(path: string, init?: RequestInit) => request<T>(config.authApiUrl, path, init);
export const projectFetch = <T>(path: string, init?: RequestInit) => request<T>(config.projectApiUrl, path, init);
```

File uploads (`FileController`) need `multipart/form-data` — don't set
`Content-Type` manually there, let the browser set the boundary:

```ts
const form = new FormData();
files.forEach((f) => form.append("Files", f));
await fetch(`${config.projectApiUrl}/api/File/upload`, { method: "POST", body: form });
```

---

## 7. Endpoint catalogue

### AuthenticationAPI — base `VITE_AUTH_API_URL` (default dev port `48988`)

Swagger: `{authApi}/swagger`. No global auth requirement on any endpoint
today (see §3).

| Method | Route | Body / Query | Response |
|---|---|---|---|
| POST | `/api/User/login` | `LoginCommand { userName, password }` | `LoginResponse { accessToken, isAutheticated, message, user: UserResponse, roles: string[] }` |
| POST | `/api/User` | `RegisterCommand { id?, userName, password, firstName, lastName, firstNameAr?, lastNameAr?, phoneNumber, email, about?, rating?, roles: string[] }` | `201` + `{ id: string }` (new user id) |
| POST | `/api/User/confirm-email?token=&userId=` | query only | `200` |
| GET | `/api/User?roleId=&rating=&userName=&pageNumber=&pageSize=` | — | `List<UserResponse>` (not paginated envelope — check handler, `GetAllUsersResponse` wraps it, confirm shape before typing) |
| GET | `/api/User/by-role/{role}` | — | users with that role |
| GET | `/api/User/multiple?Ids=id1&Ids=id2` | — | users by id list |
| GET | `/api/User/roles` | — | all roles |
| GET | `/api/User/lockout/{id}` | — | lock a user (note: `GET` used for a mutation — backend quirk, keep as-is) |
| GET | `/api/User/unlock/{id}` | — | unlock a user |
| POST | `/api/User/reset-password` | `ResetPasswordCommand { userId, newPassword, confirmPassword }` | `200` |
| DELETE | `/api/User/{id}` | — | `{ success, message }` |
| PUT | `/api/User/{id}` | `UpdateUserCommand { firstName?, lastName?, email?, phoneNumber?, firstNameAr?, lastNameAr?, about? }` | `{ success, message }` |
| POST | `/anonym-users/request-code` | `{ phoneNumber }` | `204` |
| POST | `/anonym-users/login` | `{ code }` | JWT string |

`UserResponse` fields: `id, userName, firstName, lastName, firstNameAr, lastNameAr, phoneNumber, email, about?, rating, lockoutEnd?`.

**Frontend mapping**: `authApi.login` → `POST /api/User/login`, using
`userName` as the email/username field (the mock currently accepts an
`email` param — decide whether real usernames are emails or separate;
check `RegisterCommand.UserName` usage server-side, the seed users use
emails as usernames so this is likely fine as-is).

### ProjectAPI — base `VITE_PROJECT_API_URL` (default dev port `48988`, **move to `48989`**, see §1)

Swagger: `{projectApi}/swagger`. JWT Bearer auth configured; see §3 for
which endpoints actually enforce it today.

#### Projects — `ProjectController`, prefix `/api/Project`

| Method | Route | Body / Query | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/Project` | `CreateProjectCommand { name, location, address?, description?, module3DLink?, images: string[], type?, quartierId?, quartierName?, quartierDescription?, quartierImages? }` | `CreateProjectResponse` | `projectsApi.create` |
| GET | `/api/Project?Name=&Location=&UserId=&Address=&Status=&MaxSellableSurfaceRange=&PageNumber=&PageSize=` | — | `PaginatedResponse<ProjectResponse>` | `projectsApi.list` |
| PUT | `/api/Project/{id}` | `UpdateProjectCommand { id, name, location, address?, description?, module3DLink?, images, type?, statusGlobal?, overallProgress?, quartierId?, quartierName?, quartierDescription?, quartierImages? }` | `ProjectResponse` | `projectsApi.update` |
| DELETE | `/api/Project/{projectId}` | — | — | `projectsApi.remove` |
| POST | `/api/Project/Like` | `AddLikedProjectCommand { userId, projectId }` | `LikedProjectResponse` | (not in current mock — add if "like" feature is wired) |
| DELETE | `/api/Project/DisLikeProject` | `RemoveLikedProjectCommand { userId, projectId }` | — | — |
| GET | `/api/Project/LikedProjects?UserId=&ProjectId=&PageNumber=&PageSize=` | — | `PaginatedResponse<LikedProjectResponse>` | — |
| PUT | `/api/Project/LikedProject` | `UpdateLikedProjectCommand { id, projectId }` | `LikedProjectResponse` | — |
| POST | `/api/Project/features` | `AddProjectFeatureCommand { projectId, features: [{name, icon}] }` | `bool` | (add-feature UI not built yet) |
| DELETE | `/api/Project/RemoveFeatures` | `RemoveProjectFeatureCommand { projectId, featureIds: Guid[] }` | `bool` | — |
| GET | `/api/Project/features?ProjectId=` | — | `List<ProjectFeatureResponse>` | `projectsApi.listFeatures` |
| POST | `/api/Project/quartiers` | `CreateQuartierCommand { name, description, images }` | `CreateQuartierResponse` | — |
| GET | `/api/Project/quartiers?Name=&PageNumber=&PageSize=` | — | `PaginatedResponse<QuartierListItem>` | `projectsApi.listQuartiers` (mock currently returns unpaginated array — adapt) |
| GET | `/api/Project/quartiers/{id}` | — | `GetQuartierByIdResponse` | — |
| POST | `/api/Project/{projectId}/videos` | `CreateEspaceTempsReelCommand { videoLink, insertedAt? }` (ProjectId from route) | `CreateEspaceTempsReelResponse` | — (not modeled in mock) |
| GET | `/api/Project/videos/{id}` | — | video by id | — |
| GET | `/api/Project/{projectId}/videos` | — | `List<VideoListItem>` | — |
| GET | `/api/Project/user/{userId}?pageNumber=&pageSize=` | — | user's purchases (`PurchaseSummaryResponse`) | — (not modeled; would back a "My Purchases" buyer view) |
| POST | `/api/Project/{projectId}/type-biens` | `AssociateTypeBienToProjectCommand { typeBienId }` (ProjectId from route) | — | — |
| GET | `/api/Project/{projectId}/type-biens` | — | `List<TypeBienListItem>` | (project detail page currently reads `project.typeBiens` from the project payload itself — real API needs this separate call, or rely on `ProjectResponse.TypeBiens` which is already embedded) |
| GET | `/api/Project/type-biens` | — | all type-biens tied to any project | close to `typeBiensApi.list`, but that hits `TypeBiensController` instead (see below) — pick one source of truth |

`ProjectResponse` fields (already matches frontend `Project` type closely):
`id, name, location, address, agentId?, agentPhoneNumber?, images: string[], description, module3DLink, type?, statusGlobal?, overAllProgress: decimal, numberLikes: long, quartierName?, quartierDescription?, quartierImages?, isLiked, typeBiens: TypeBienListItem[], assignedAgents: AgentDTO[], assignedNotaries: NotaryDTO[]`.

#### Immeubles & Units — `ImmeubleController`, prefix `/api/Immeuble`

| Method | Route | Body / Query | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/Immeuble` | `CreateImmeubleCommand { name, projectId, location, type, minPrice, maxPrice, images: string (single string, not array!), description, latitude, longitude, status, numberOfUnits, minSellableSurfaceRange, maxSellableSurfaceRange, module3DLink?, numberOfSoldUnites, numberOfAvailableUnites, sellsPercentage }` | `CreateImmeubleResponse { projectId, message }` | `immeublesApi.create` |
| GET | `/api/Immeuble?Name=&Location=&ProjectId=&Type=&MinPrice=&MaxPrice=&MinSellableSurfaceRange=&MaxSellableSurfaceRange=&PageNumber=&PageSize=` | — | `PaginatedResponse<ImmeubleResponse>` | `immeublesApi.list` |
| GET | `/api/Immeuble/{id}` | — | `ImmeubleResponse` | `immeublesApi.getById` |
| PUT | `/api/Immeuble/{id}` | `UpdateImmeubleCommand { name, location, type, minPrice, maxPrice, minSellableSurfaceRange, maxSellableSurfaceRange, status (int enum), images: string, description, latitude, longitude }` | `ImmeubleResponse` | `immeublesApi.update` |
| DELETE | `/api/Immeuble/{id}` | — | `{ success, message }` | `immeublesApi.remove` |
| POST | `/api/Immeuble/{immeubleId}/units` | `CreateProjectUnitCommand { floor, unitNumber, numberOfBedrooms, numberOfBathrooms, apartmentSurface, balconySurface?, terraceSurface?, gardenSurface?, view, orientation, totalSurface, price? }` | `CreateProjectUnitResponse` | (add-unit UI not built yet) |
| GET | `/api/Immeuble/by-immeuble?ImmeubleId=&PageNumber=&PageSize=` | — | `PaginatedResponse<UnitResponse>` | `immeublesApi.listUnits` (mock returns a flat array — adapt to unwrap `.data`) |
| GET | `/api/Immeuble/all?Floor=&MinBedrooms=&MaxBedrooms=&MinSurface=&MaxSurface=&MinPrice=&MaxPrice=&ProjectId=&ImmeubleId=&PageNumber=&PageSize=` | — | `PaginatedResponse<UnitResponse>` | `immeublesApi.listAllUnits` |
| PUT | `/api/Immeuble/update/{id}` | `UpdateUnitCommand { floor, unitNumber, numberOfBedrooms?, numberOfBathrooms?, apartmentSurface?, balconySurface?, terraceSurface?, gardenSurface?, view, orientation, totalSurface?, saleableValue?, saleableValue1?, priceSaleableValue?, priceSaleableValue1?, latestPrice?, status }` | `{ isSuccess, message }` | `immeublesApi.updateUnit` |
| POST | `/api/Immeuble/features` | `AddImmeubleFeatureCommand { immeubleId, features: [{name, icon}] }` | `bool` | (add-feature UI not built yet) |
| GET | `/api/Immeuble/features?ImmeubleId=` | — | `List<ImmeubleFeatureResponse>` | `immeublesApi.listFeatures` (mock is a static global list, not per-immeuble — must pass `immeubleId`) |
| POST | `/api/Immeuble/{immeubleId}/tracking` | `AddImmeubleTrackingCommand { statusUpdate }` | `{ message }` | — (construction progress log, not in mock) |
| GET | `/api/Immeuble/{immeubleId}/tracking` | — | `List<ImmeubleTrackingResponse>` | — |

⚠️ `CreateImmeubleCommand.Images` and `UpdateImmeubleCommand.Images` are a
**single `string`**, while `ImmeubleResponse.Images` returned on read is a
**`List<string>`**. The backend likely expects a delimited string (check
the handler / `ImmeubleConfiguration` for how it splits) — don't assume
JSON array on write even though you get an array back on read.

`Unit` status (`UpdateUnitCommand.Status`) is a plain string — backend
doesn't enforce `Available/Reserved/Sold` via enum, but keep those values
for consistency with `UnitResponse` usage elsewhere.

#### Type Biens — `TypeBiensController`, prefix `/api/TypeBiens`

| Method | Route | Body | Response |
|---|---|---|---|
| POST | `/api/TypeBiens` | `CreateTypeBienCommand { name, description?, image?, price?, nbrChambre?, nbrSalleDeBain?, minSurface?, maxSurface?, imagesInterieur? }` | `CreateTypeBienResponse { id, message }` |
| PUT | `/api/TypeBiens/{id}` | `UpdateTypeBienCommand { name?, description?, image?, price?, nbrChambre?, nbrSalleDeBain?, minSurface?, maxSurface?, imagesInterieur? }` | `UpdateTypeBienResponse` |
| DELETE | `/api/TypeBiens/{id}` | — | `DeleteTypeBienResponse` |

⚠️ There is **no `GET /api/TypeBiens` list endpoint on this controller.**
The frontend's `typeBiensApi.list()` needs to hit
`GET /api/Project/type-biens` instead (returns `TypeBienListItem[]`,
richer than the mock's `{id, name, description}` — it also includes price,
room counts, surface range, images). Update `TypeBiensApi.list`'s return
type to match `TypeBienListItem` or keep a narrower projection.

`TypeBienListItem` (from `GetTypeBiensByProjectQuery`/`GetTypeBiensByImmeubleQuery`):
`id, name, description?, image?, price?, nbrChambre?, nbrSalleDeBain?, minSurface?, maxSurface?, surfaceRange?, imagesInterieur: string[]`.

#### Reservations — `ReservationsController`, prefix `/api/Reservations`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/Reservations/create` | `CreateReservationCommand { buyerId?, name?, lastName?, cin?, email?, phoneNumber?, unitId, agentId?, notaireId?, totalPropertyPrice, reservationAmount, isUnderConstruction }` | `CreateReservationResponse { reservationId, message, success, details }` | `reservationsApi.create` |
| GET | `/api/Reservations/{id}` | — | `GetReservationByIdResponse` | `reservationsApi.getById` |
| GET | `/api/Reservations/list?BuyerId=&Name=&LastName=&CIN=&Email=&UnitId=&AgentId=&NotaireId=&IsUnderConstruction=&PageNumber=&PageSize=` | — | `PaginatedResponse<GetReservationsResponse>` | `reservationsApi.list` (note: `status` filter param doesn't exist server-side — filter client-side, or ask backend to add it) |
| POST | `/api/Reservations/{id}/approve` | `ApproveReservationCommand { adminUserId, adminNote?, documents: [{fileName,url,contentType?,sizeBytes?,documentType?}] }` (ReservationId from route) | `bool` | `reservationsApi.approve` |
| POST | `/api/Reservations/{id}/reject` | `RejectReservationCommand { adminUserId, reason? }` | `bool` | `reservationsApi.reject` |
| PUT | `/api/Reservations/{id}/assign-notaire` | `AssignNotaireToReservationCommand { notaireId? }` | `AssignNotaireToReservationResponse` | `reservationsApi.assignNotaire` |
| PUT | `/api/Reservations/{id}/cancel` | `CancelReservationCommand {}` (id from route only) | `CancelReservationResponse` | `reservationsApi.cancel` (mock passes a note — real command has no note field, drop it or extend backend) |
| PUT | `/api/Reservations/{id}/sold` | `SoldReservationCommand {}` | `SoldReservationResponse` | `reservationsApi.markSold` |

`adminUserId` on approve/reject should come from the logged-in admin's id
(`useAuth().user.id`), not hardcoded.

#### Sales — `SalesController`, prefix `/api/sales`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/sales` | `CreateSaleCommand { buyerId? \| buyerFirstName/buyerLastName/buyerEmail/buyerPhoneNumber/buyerCIN, unitId, saleDate, totalPrice, isUnderConstruction, initialPaymentAmount?, reservationId? }` | `CreateSaleResponse { saleId, purchaseId, message }` | — (no "create sale" UI built yet, only payments/summary) |
| POST | `/api/sales/{saleId}/payments` | `AddPaymentCommand { amountPaid, paymentDate, status }` (SaleId from route) | `bool` | `salesApi.addPayment` (mock signature is `(saleId, amount, note?)` — real command wants `amountPaid`/`paymentDate`/`status`, no free-text note) |
| GET | `/api/sales/user/{userId}` | — | `UserSalesResponse { sales: SaleItem[], totalPrice, totalPaid, totalRemaining }` | `salesApi.getByUser` |

⚠️ `SaleItem` fields are `saleId, unitId, saleDate, totalPrice, paid, remaining`
— **no** `projectName`/`unitDetails`/`payments` array like the mock has.
The real API doesn't return per-payment history or human-readable
project/unit labels from this endpoint; you'd need to join against
`GetUnitsByProjectId`/`ImmeubleController` results (or ask the backend to
enrich the response) to reproduce the current mock UI's "Paiements —
{unitDetails}" breakdown per sale.

#### After-sales Claims — `AfterSaleClaimsController` (class name), prefix `/api/after-sales/claims`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/after-sales/claims` | `CreateAfterSaleClaimCommand { unitId, buyerId?, guestName?, guestEmail?, guestPhone?, title, description, category (int), priority (int), files: [{url, fileName?, contentType?, sizeBytes?}] }` | `{ id: guid }` | `claimsApi.create` |
| GET | `/api/after-sales/claims?BuyerId=&AgentId=&UnitId=&Status=&From=&To=&PageNumber=&PageSize=` | — | `PaginatedResponse<AfterSaleClaimResponse>` | `claimsApi.list` |
| PUT | `/api/after-sales/claims/{claimId}/status` | `UpdateClaimStatusCommand { newStatus (int), changedByUserId, note?, resolutionSummary?, proofs: FileDto[] }` | `204` | `claimsApi.updateStatus` |

`AfterSaleClaimResponse`: `id, unitId, title, category (int), priority (int), status (int), createdAt, resolvedAt?, attachmentUrls: string[]`.
Note there's no `buyerId` in the *response* even though it's on create —
if the UI needs to show who filed it, resolve `buyerId` separately via
`UserController`.

#### Deliveries — `DeliveriesController`, prefix `/api/deliveries`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/deliveries` | `ScheduleDeliveryCommand { saleId, unitId, deliveryDate, status, report }` | `Guid` (new delivery id) | `deliveriesApi.schedule` |
| PATCH | `/api/deliveries/{deliveryId}/status` | `UpdateDeliveryStatusCommand { status, report }` | `bool` | `deliveriesApi.updateStatus` (mock signature has no `report` — add a text field to the UI, or default it) |

⚠️ **There is no `GET /api/deliveries` list endpoint.** `deliveriesApi.list()`
in the mock has nothing to call — either ask the backend team to add a
list/query endpoint (mirroring the `GetReservationsQuery` pattern), or
derive the deliveries list from `sales/user/{userId}` + something else in
the interim. Flag this as a backend gap.

#### Appointments — `AppointmentsController`, prefix `/api/appointments`

| Method | Route | Auth | Body | Response | Frontend interface |
|---|---|---|---|---|---|
| POST | `/api/appointments` | `[AllowAnonymous]` | `CreateAppointmentCommand { projectId, agentId?, appointmentDate, propertyType?, userId? (filled if authenticated), name, lastName, email, phoneNumber, typeBienIds: number[] }` | `CreateAppointmentResponse` | `appointmentsApi.create` |
| GET | `/api/appointments?ProjectId=&AgentId=&AppointmentDate=&UserId=&Status=&Name=&LastName=&Email=&PhoneNumber=&PageNumber=&PageSize=` | `[AllowAnonymous]` | — | `PaginatedResponse<AppointmentResponse>` | `appointmentsApi.list` |
| PATCH | `/api/appointments/{appointmentId}/status` | `[AllowAnonymous]` (commented `[Authorize(Roles="Agent")]`) | `UpdateAppointmentStatusCommand { status }` | `UpdateAppointmentStatusResponse` | `appointmentsApi.updateStatus` |
| GET | `/api/appointments/user/{userId}` | **requires Bearer + role `Acheteur,Admin,Agent`** | — | `List<AppointmentResponse>`? verify | `appointmentsApi.byUser` |
| GET | `/api/appointments/project/{projectId}` | **requires Bearer + role `Acheteur,Admin,Agent`** | — | list | `appointmentsApi.byProject` |

Note `CreateAppointmentCommand.AppointmentDate` (not `Date`) and it takes
`ProjectId` as non-nullable `Guid` (the public booking form must always
supply a project). `AppointmentResponse.Date` vs `.AppointmentDate` are
**both present** on the response DTO (looks like a leftover duplicate
field) — read `appointmentDate` as the source of truth, `date` may be
stale/unset depending on the handler.

#### Notary Appointments — `NotaryAppointmentsController`, prefix `/api/NotaryAppointments`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/NotaryAppointments` | `CreateNotaryAppointmentCommand { buyerId?, notaireId?, agentId?, connectedUserId?, reservationId, appointmentDate, status="Scheduled", taxFees, tahfidFees }` | `CreateNotaryAppointmentResponse` | `notaryApi.createAppointment` |
| GET | `/api/NotaryAppointments/{id}` | — | `GetNotaryAppointmentByIdResponse` (includes buyer name/CIN/email/phone) | `notaryApi.getAppointmentById` |
| GET | `/api/NotaryAppointments?ReservationId=&BuyerCIN=&NotaireId=&AgentId=&Status=&PageNumber=&PageSize=` | — | `PaginatedResponse<NotaryAppointmentResponse>` | `notaryApi.listAppointments` |
| GET | `/api/NotaryAppointments/NotaireAvailability?...` | check handler params | availability slots | `notaryApi.availability` (mock fabricates weekday availability — real endpoint name is `NotaireAvailability`, params not fully captured here, inspect `GetNotaryCalendarQuery`/handler before wiring: likely `notaryId`, `from`, `to`) |
| PUT | `/api/NotaryAppointments/{id}` | `UpdateNotaryAppointmentCommand { status?, taxFees?, tahfidFees? }` | `UpdateNotaryAppointmentResponse` | `notaryApi.updateAppointment` |

Controller class is `[AllowAnonymous]` (commented-out `[Authorize(Roles = "Admin,Notaire")]`).

`CreateNotaryAppointmentCommand` has **no `PropertyPrice` field** (unlike
the response, which has `propertyPrice`) — it's presumably derived
server-side from the reservation. Don't try to send it.

#### Notary Blocks — `NotaryBlocksController`, prefix `/api/notaries/{notaryId}/blocks`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/notaries/{notaryId}/blocks` | `CreateNotaryBlockCommand { notaryId, start, end, reason }` (NotaryId from route, but also in body per the command shape — send both) | `CreateNotaryBlockResponse` | `notaryApi.createBlock` |
| DELETE | `/api/notaries/{notaryId}/blocks/{blockId}` | — | `bool` | `notaryApi.deleteBlock` |
| GET | `/api/notaries/{notaryId}/blocks?from=&to=` | — | blocks in range | `notaryApi.listBlocks` (mock ignores date range — add `from`/`to` params) |

⚠️ Field names differ: backend command uses **`Start`/`End`**, the
frontend's `NotaryBlock` type uses **`from`/`to`**. Rename in the adapter,
not in the shared type (keep the type name generic/consistent with the
rest of the app; translate at the http boundary).

#### Project Assignments — `ProjectAssignmentController`, prefix `/api/ProjectAssignment`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/ProjectAssignment` | `CreateProjectAssignmentCommand { projectId, agentId?, notaryId?, isActive=true }` | `CreateProjectAssignmentResponse` | `assignmentsApi.create` |
| DELETE | `/api/ProjectAssignment/UnassignAgentsNotaire` | `UnassignProjectAssignmentCommand { assignmentId }` | `UnassignProjectAssignmentResponse` | (mock's `remove` maps closer to this than the hard delete below — prefer this one, it's a soft-deactivate) |
| PUT | `/api/ProjectAssignment` | `UpdateProjectAssignmentCommand { id, agentId?, notaryId?, projectId?, isActive? }` | `UpdateProjectAssignmentResponse` | `assignmentsApi.update` |
| DELETE | `/api/ProjectAssignment/{id}` | — | `DeleteProjectAssignmentResponse` | `assignmentsApi.remove` (hard delete) |
| GET | `/api/ProjectAssignment/{id}` | — | `GetProjectAssignmentByIdResponse` | `assignmentsApi.getById` |
| GET | `/api/ProjectAssignment?projectId=&agentId=&pageNumber=&pageSize=` | — | `PaginatedResponse<GetProjectAssignmentByIdResponse>` | `assignmentsApi.list` |

Note the command field is **`NotaryId`** (backend), while the frontend
type uses **`notaireId`** (French spelling, matches the rest of the
reservation/notary domain) — normalize at the http-adapter boundary.

#### Admin Dashboard — `AdminDashboardController`, prefix `/api/AdminDashboard`

| Method | Route | Query | Response |
|---|---|---|---|
| GET | `/api/AdminDashboard/overview?Year=&Month=` | `AdminDashboardQuery` | `AdminDashboardResponse { totalAgents, totalProjects, salesThisMonth, salesVolumeThisMonth, agentsPerformance: AgentPerformanceDto[], topPerformers: AgentPerformanceDto[] }` |

⚠️ `AgentPerformanceDto` fields are **`agentFullName`** (not `agentName`),
plus extra fields the mock doesn't have: `leadsGenerated`,
`appointmentsScheduled`, `successfulSales` (the mock only has
`appointmentsCount`). Rename `agentFullName→agentName` in the adapter or
update the shared type — and decide whether to surface the extra KPIs in
`DashboardPage.tsx`.

The dashboard query takes `Year`/`Month`, not the mock's
`"month"|"quarter"|"year"` timeframe string — `dashboardApi.overview()`
needs a real signature change (pass a year/month pair, or omit for
current period — check what the handler defaults to when both are null).

#### Feedback — `FeedbackController`, prefix `/api/Feedback`

| Method | Route | Body | Response | Frontend interface |
|---|---|---|---|---|
| POST | `/api/Feedback/submit-feedback` | `SubmitFeedbackCommand { userId, projectId?, rating, comments, attachements: string[] }` (note backend typo `Attachements`) | `string` (message) | `feedbackApi.submit` |
| GET | `/api/Feedback/{feedbackId}` | — | `FeedbackDetailsResponse { feedbackId, user: UserDto, project?: ImmeubleResponse, rating, comments, attachments, createdAt }` | `feedbackApi.getById` |
| GET | `/api/Feedback/feedbacks?UserId=&ProjectId=&AgentId=&PageNumber=&PageSize=` | — | `PaginatedResponse<FeedbackDetailsResponse>` | `feedbackApi.list` |

⚠️ `FeedbackDetailsResponse.project` is a full **`ImmeubleResponse`**, not
`{id, name}` like the mock's `Feedback.project`. And `.user` is a
`UserDto {id, userName, email}` — no `fullName` field, so the UI must
compose a display name from elsewhere (or the backend needs a `FirstName`/`LastName`
added to `UserDto`, or resolve the buyer via `UserController` separately).

#### Files — `FileController`, prefix `/api/File`

| Method | Route | Body | Response |
|---|---|---|---|
| POST | `/api/File/upload` | `multipart/form-data`, field name `Files` (multiple) | `UploadFilesResponse` (list of URLs — check response shape, likely `{ fileLinks: string[] }` or similar) |
| DELETE | `/api/File/delete/{fileName}` | — | `{ isSuccess, message }` |
| PUT | `/api/File/update/{fileName}` | `multipart/form-data`, field `File` | `{ isSuccess, fileLink, message }` |
| GET | `/api/File/download/{fileName}` | — | raw file bytes |

Not modeled in the current frontend at all — needed once image upload
(project/immeuble photos, claim attachments, reservation documents) is
wired instead of using `picsum.photos` placeholder URLs.

---

## 8. Recommended backend tweaks (optional, but will simplify the frontend)

If you have write access to the backend and want the integration to be
less error-prone, consider these small changes — none are required, the
frontend can absolutely adapt to the current shapes, but these remove
whole categories of adapter code:

1. Add `options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())`
   in both `Program.cs` files (`AddControllers`/`AddMvcCore` json options) so
   `ReservationStatus`, `ClaimStatus/Category/Priority`, `ProjectStatus`
   serialize as strings instead of numbers — removes the whole int↔string
   mapping table in §4.
2. Add a `GET /api/TypeBiens` list endpoint (currently only
   Create/Update/Delete exist on that controller; list only exists nested
   under Project/Immeuble).
3. Add a `GET /api/deliveries` list/query endpoint (currently create +
   update-status only, no way to list them).
4. Add a `status` query filter to `GET /api/Reservations/list` (currently
   must filter client-side).
5. Make `CreateImmeubleCommand.Images`/`UpdateImmeubleCommand.Images` a
   `List<string>` to match the response shape, instead of a single
   delimited string.

None of these block wiring — they just mean writing a bit more
translation code in `src/api/http/*` if left as-is.

---

## 9. Suggested order of work for Claude Code

1. Fix the port collision (§1), start both APIs, confirm `/health` and
   `/swagger` on each.
2. Add `.env` with both base URLs (§2).
3. Build `src/api/http/client.ts` (§6) with the Bearer-token fetch wrapper.
4. Implement `src/api/http/authApi.ts` and wire `AuthContext` to store the
   real JWT (localStorage or memory) and attach it to `projectFetch`.
5. Implement the remaining `src/api/http/*.ts` files one per interface in
   `src/api/interfaces.ts`, using the endpoint tables in §7 — go in the
   order the admin sidebar lists them (Dashboard → Projects → Immeubles →
   TypeBiens → Reservations → Sales → Appointments → Notary → Deliveries →
   Claims → Feedback → Assignments), since later features often need ids
   produced by earlier ones (e.g., can't test Reservations without a real
   Unit id from Immeubles).
6. Add the enum↔string adapters from §4 in one shared `src/api/http/enums.ts`
   so every http adapter reuses the same mapping tables instead of
   duplicating them.
7. Flip `src/api/index.ts` from `./mock/mockApi` to `./http`.
8. Manually smoke-test each admin page against the running backend;
   cross off the ⚠️ items in §7 as they're resolved (some need either a
   backend tweak or a UI field added, per the notes above).
