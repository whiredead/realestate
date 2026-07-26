# Backend domain-model & vocabulary audit

> **STATUS — updated after work order #4 (2026-07-26).** The first tranche is implemented: the sale
> workflow now enforces itself. Resolved: **B3, B4, B5, B6, B6b, B7, B8, B19** (7 of the 11 BLOCKERs).
> Partially resolved: **B6c**. Everything else stands as written. See
> [Work order #4 — what changed](#work-order-4--what-changed) at the end of this document.
>
> The body below is the **original audit text**, kept as the record of what was found. Headings carry
> a ✅ marker where the finding has since been closed; the description under each is the pre-fix state.

> Work order #3. Audit and plan only — no code changed, no EF migration created.
> Authority: `C:\Users\LEGION\Desktop\realestateFront\GPIA_WORKFLOW.md` (§ references below are to that file).
> Scope: the domain model and status vocabulary of `ProjectAPI` (+ `AuthenticationAPI` where identity is involved).
>
> **Deliberately not covered** (already done elsewhere, per the work order):
> §6 access control and the role model — see `RBAC_PROGRESS.md` (steps 1–6 done, 7–10 open) and
> `SPEC_COMPLIANCE_AUDIT.md` (P0/P1 fixed). Nothing below re-audits authorization.
>
> Severity: **BLOCKER** = wrong domain model or a missing gate · **MAJOR** = wrong vocabulary or an
> unenforced constraint · **MINOR** = naming.

---

## Executive summary

Two findings dominate and are worth stating before the detail:

1. **The unit commercial state machine — "the backbone of the whole sale workflow" (§3) — does not
   exist.** `Unit.Status` is a free-text string with three values, and it is written in exactly **one**
   place in the entire codebase: the generic `UpdateUnit` CRUD handler. No reservation, notary or
   handover command ever touches it. Approving a reservation does not make the unit `RESERVED`;
   a completed sale does not make it `SOLD`. (B3, B4)

2. **The "one active reservation per unit" index does not exist.** Three code comments and the
   exception filter name `IX_Reservations_ActivePerUnit` as "the concurrency backstop", but it is
   absent from the EF model, every migration and every SQL script. The only enforcement is a
   read-then-write check inside the handler — a TOCTOU race that the doc specifically says must be
   closed by a row lock plus a partial unique index (§3, §6.3). (B5)

Both are cheap to fix **now** and expensive later: the unit-status change rewrites a column's whole
value domain, and the unique index cannot be created at all once conflicting rows exist.

The genuinely good news, which shapes the plan: the **final-visit cluster** (case / attempt / report /
snag / history / eligibility calculator), the **payment ledger** (immutable entries, reversal FK,
allocations, filtered "one ACTIVE schedule" index), the **title state + history**, and the
**reservation state machine** are all present and closely conformant to §5.3–§5.6. The gaps are
concentrated in the older parts of the model, not spread evenly.

---

## 1. Property hierarchy (§3)

### B1 — No `Floor` entity — **BLOCKER**
**Exists.** `ProjectAPI.Domain.Immeubles.Entities.Unit.Floor` is `string`, `HasMaxLength(50)`
(`UnitConfiguration.cs:24`). There is no `Floor` entity, no `Floors` table, no `FloorId`.
**Doc requires.** §3: `Neighborhood → Project → Building → Floor → Unit`, and a unit's project derived
**only** via `unit.floor_id → floor.building_id → building.project_id`. §6.2 lists
`neighborhoods 1→N projects 1→N buildings 1→N floors 1→N units`.
**Confirms** frontend gap (a) / audit finding T2.

### B2 — `Units.ProjectId` is really a building FK — **MAJOR** (naming trap)
**Exists.** `UnitConfiguration.cs:76-79` — `HasOne(unit => unit.Immeuble).WithMany(p => p.Units)
.HasForeignKey(unit => unit.ProjectId)`. The column named `ProjectId` holds an `Immeubles.Id`.
The XML doc-comment on the property compounds it: *"identifier of the related project"*.
**Doc requires.** §3: no independent, editable `project_id` on units.
**Nuance worth recording.** The doc's *rule* is accidentally satisfied — there is no second, separately
editable project pointer, because this column **is** the building link. The defect is the name, which
has already produced real bugs (noted in `RBAC_PROGRESS.md` and `GAP_ANALYSIS.md` lot 2.2) and makes
every project join look wrong. Renaming to `ImmeubleId` is cosmetic in behaviour and high-value in
readability. **Confirms** frontend gap (b).

### B3 — `Unit.Status` is free text with 3 of the doc's 8 states — **BLOCKER** ✅ RESOLVED (work order #4, step 3)
**Exists.** `Unit.cs:114` — `public string Status { get; set; } = "Available"; // e.g., "Available", "Sold"`.
No enum, no CHECK constraint, no state machine, no status history table.
**Doc requires.** §3's eight states — `AVAILABLE, HOLD_PENDING_APPROVAL, RESERVED, CONTRACTED, SOLD,
DELIVERED, SUSPENDED, CANCELLED` — with transitions only via commands, and "returning from
RESERVED/CONTRACTED/SOLD to AVAILABLE is **never** automatic".
Missing `HOLD_PENDING_APPROVAL` makes the submit→hold step unrepresentable; missing `DELIVERED`
removes the SAV precondition (§5.9 "only for DELIVERED units").

### B4 — No lifecycle command ever writes the unit status — **BLOCKER** ✅ RESOLVED (work order #4, step 3)
**Exists.** A repo-wide search for writes to `unit.Status` returns exactly one hit:
`Units/UpdateUnit/UpdateUnitHandler.cs:48` — a generic CRUD field-patch handler.
Consequently: `CreateReservationHandler` does not set `HOLD_PENDING_APPROVAL`;
`ApproveReservationHandler` sets `reservation.Status = Approved` and creates the purchase but **never
touches the unit**; `SoldReservationHandler` does not set `SOLD`; nothing sets `DELIVERED`.
**Doc requires.** §3 and §4 steps 5–12: submit ⇒ unit `HOLD_PENDING_APPROVAL`; approve ⇒ unit
`RESERVED` (§5.3 "Approve ⇒ unit RESERVED"); notary `PURCHASE_COMPLETED` ⇒ reservation `CONVERTED`
**and** unit `SOLD` in one transaction (§5.7); handover report validated ⇒ unit `DELIVERED` and the
warranty starts (§5.8).
**Practical effect.** The only way a unit's commercial status can change today is an administrator
hand-editing it through `PUT /api/Immeuble/update/{id}` — precisely the affordance work order #2
removed from the frontend UI (finding L2). With that control gone, unit status is now effectively
frozen at its seeded value.

### B19 — Generic CRUD handlers accept direct status writes — **BLOCKER** (§7) ✅ RESOLVED (work order #4, steps 3 & 5)
**Exists.** `UpdateUnitHandler.cs:48` (`unit.Status = request.Status`),
`UpdateProjectHandler.cs:51` (`project.StatusGlobal = request.StatusGlobal ?? …`),
`UpdateImmeubleHandler.cs:49` (`immeuble.Status = request.Status!.Value.ToString()`).
All three are plain `PUT` field patches with no transition matrix, no history row, no audit entry.
**Doc requires.** §7: "Status transitions happen only via explicit **commands** … inside a DB
transaction that also writes status history, audit log, and an outbox event. Direct `status` updates
are forbidden."
**Contrast.** The reservation, snag, title and report flows *do* go through their state machines —
this finding is specific to the three legacy CRUD handlers.

---

## 2. One active reservation per unit (§3, §6.3)

### B5 — The partial unique index does not exist — **BLOCKER** ✅ RESOLVED (work order #4, step 2)
**Exists.** `ReservationConfiguration.cs` declares **no** index at all; the EF model snapshot shows only
a plain non-unique `b.HasIndex("UnitId")`. Grepping every `.sql` file and migration for
`IX_Reservations_ActivePerUnit` returns **nothing**.
Meanwhile the name is referenced as if it were real in four places:
`ReservationStateMachine.cs:71` ("MUST stay in sync with the filtered index … Status IN (0, 1, 4, 6)"),
`CreateReservationHandler.cs:36`, `ResubmitReservationHandler.cs:13`, and
`ApiExceptionFilter.cs:102`, which inspects `SqlException` messages for that index name to translate a
unique violation into `UNIT_NOT_AVAILABLE`. That translation branch is currently unreachable.
**Doc requires.** §3: "reservation submission + unit hold happen in one DB transaction with a row lock;
a partial unique index guarantees ≤1 active reservation per unit (active = `SUBMITTED,
CHANGES_REQUESTED, APPROVED, CONVERTED`)". §6.3 repeats it as a critical constraint.
**Actual enforcement today.** `CreateReservationHandler.cs:38-45` runs a `.AnyAsync(...)` check over the
four blocking statuses and throws if one exists — correct logic, but read-then-write with no
transaction and no row lock. Two concurrent submits on the same unit both pass the check and both
commit.
**Note.** The equivalent index *is* correctly implemented for payment schedules
(`IX_PaymentSchedules_ActivePerReservation`, filtered `[Status] = 1`), so the pattern is already
established in this codebase — it was simply never applied to reservations.

---

## 3. Project lifecycle (§3)

### B6 — `AllowsFinalVisit` is unlocked by a commercial fact — **BLOCKER** ✅ RESOLVED (work order #4, step 1)
**Exists.** `Project.StatusGlobal` is free text defaulting to the misspelled `"CommingSoon"`
(`Project.cs:48`). `ProjectStatusCodes` (`Construction/Entities/ProjectStatusCodes.cs`) does define all
six §3 codes and a `Normalize()` — a good structure — but its mapping table reads:

```csharp
"COMPLETED" or "AVAILABLE" or "DELIVERED" => Completed,   // line 43
"ARCHIVED" or "SOLD" => Archived,                          // line 45
```

and `AllowsFinalVisit(status) => Normalize(status) == Completed` (line 54) is the **only** gate on
`RequestFinalVisitCommand.cs:80`, which throws `PROJECT_NOT_COMPLETED` otherwise.
**Doc requires.** §3: final visits allowed only when the project is `COMPLETED`, which "requires 100%
progress, real end date, explicit confirmation, audit entry".
**Why this is a BLOCKER.** Legacy `"Available"` means *units are on sale* — it says nothing about
construction being finished. Every legacy project stored as `Available` therefore reports as
construction-complete and passes the final-visit gate. A buyer can request a final visit on an unbuilt
project. This is the identical defect the senior engineer rejected in the frontend mapping this week
(frontend commit `dd7fbf1`); the frontend is now correct and **the backend still has it**. The
principle applies here with more force, because this is the side that actually enforces the gate.

### B6b — `DRAFT` is unreachable at creation — **MAJOR** ✅ RESOLVED (work order #4, step 5)
**Exists.** `CreateProjectHandler.cs:58` — `StatusGlobal = request.StatusGlobal ?? "ComingSoon"`.
`ProjectStatusCodes.Draft` is defined but never assigned anywhere.
**Doc requires.** A project is created in draft (FR-CMS-001; §3 lifecycle starts at `DRAFT`).
**Effect.** There is no unpublished state — a newly created project is immediately `PLANNED`.

### B6c — Column never migrated to the canonical codes — **MAJOR** ⚠️ PARTIALLY RESOLVED (work order #4, step 5)
`ProjectStatusCodes` normalises **on read**, but the stored column keeps legacy spellings (including
the misspelling). `GetAllProjectsQuery` still documents its filter as `"CommingSoon", "Available",
"Deleted"` — and `"Deleted"` is not a value any layer defines. `Immeuble.Status` is separately parsed
with `Enum.Parse<ProjectStatus>(immeuble.Status)` (`GetAllImmeublesHandler.cs:56`), i.e. a *different*
enum against the same vocabulary, which throws on any unexpected value.

---

## 4. Notary outcome and conversion (§5.7)

### B7 — No outcome field distinct from status — **BLOCKER** ✅ RESOLVED (work order #4, step 4)
**Exists.** `NotaryAppointment.Status` is `string` (`HasMaxLength(50)`, comment: *"e.g., Scheduled,
Completed, Cancelled"*). There is no outcome column, no `PreviousAppointmentId`, and the entity does
not use the `AppointmentAttemptStatus` machine that already exists in `FinalVisits`.
**Doc requires.** §5.7: outcome ∈ `PURCHASE_COMPLETED / INCOMPLETE_FILE / BUYER_ABSENT / POSTPONED /
NOT_COMPLETED_OTHER`, kept **separate** from status, and "**Only `PURCHASE_COMPLETED` changes
anything**: reservation → CONVERTED and unit → SOLD atomically."
**Confirms** frontend gap (d).

### B8 — `PUT /sold` converts a reservation with no notary act at all — **BLOCKER** ✅ RESOLVED (work order #4, step 4)
**Exists.** `Reservations/SoldReservation/SoldReservationHandler.cs:24-26` — validates the transition
via the state machine (good), then sets `reservation.Status = Sold` (CONVERTED). It takes no notary
appointment, records no outcome, sets no unit status, and opens no transaction. The route is
`[Authorize(Roles = RoleGroups.AdminsNotary)]` with a comment claiming conversion "is recorded by the
notary (PURCHASE_COMPLETED) / admin" — but nothing in the handler requires or verifies a
`PURCHASE_COMPLETED` result.
**Doc requires.** §5.3: "`CONVERTED` only happens when the notary records `PURCHASE_COMPLETED` (same
transaction sets unit SOLD)."
**Cross-reference.** This is the backend endpoint behind the frontend's `markSold`, removed in work
order #2 (finding L3). The frontend no longer calls it; the endpoint remains reachable.

---

## 5. Handover / delivery (§5.8)

### B9 — No handover machine, no report, no warranty start — **BLOCKER**
**Exists.** `Sales/Entities/PropertyDelivery.cs`: `SaleId`, `UnitId`, `DeliveryDate`,
`Status` (free-text string, comment *"e.g., Scheduled, Delivered, InProgress"*), and `Report` as a
single `string`. It hangs off `Sale`, not the reservation.
**Doc requires.** §5.8 + §6.2: `reservations 1→N handover_appointments 1→N handover_reports 1→N
handover_items`; the appointment follows the shared §5.1 machine; **buyer validation of the report**
sets unit `DELIVERED`, records the delivery date, **starts the warranty period** and opens SAV — and
the operation is idempotent ("re-validating never restarts warranty or duplicates delivery").
**Missing entirely.** Handover appointments as such, report versioning, checklist items, the
acknowledgement step, the warranty trigger, and the idempotence guarantee.
**Confirms** frontend gap (e).

---

## 6. Entities from §6.2 with no table

Checked against the actual `DbSet` list and the EF configuration folder.

### B10 — `crm_contacts` — **BLOCKER**
No table, no entity. A contact is not separable from a user account anywhere in the model:
`Reservation` embeds `Name/LastName/CIN/Email/PhoneNumber` inline and its `BuyerId` is an
`AspNetUsers.Id`; `Appointment` separately embeds `Name/LastName/Email/PhoneNumber` for guests;
`AfterSaleClaim` has a third copy (`GuestName/GuestEmail/GuestPhone`); `Sale` a fourth
(`BuyerFirstName/…`). `Lead` is the nearest thing but is a project-scoped lead record, not a person.
**Doc requires.** §1.1 — the foundational rule: `crm_contacts` is a business person who may have no
login; `users.crm_contact_id` is unique-when-non-null; **one person keeps the same contact forever**
through `PROSPECT → BUYER → DELIVERED_OWNER`; phone is not unique, so a match raises a duplicate alert
(§9: never an auto-merge). §6.2 hangs `consent_records, contact_interests, favorites, lead_assignments,
crm_tasks, interaction_logs, duplicate_candidates` off it — none of which exist.
**Answering the work order's question directly:** no, a contact is **not** separable from a user
account today. Identity is duplicated in four places with no link between them.

### B11 — `reservation_buyers` — **BLOCKER**
No table. `Reservation` has a single scalar `BuyerId`. No co-buyers, no `ownership_percent`, no
`access_status`.
**Doc requires.** §1.2 — "A buyer's access to a purchase file comes from **`reservation_buyers` rows,
not from the BUYER role**"; §5.3 — co-buyers are distinct contacts and ownership percentages must
total 100%; §6.2 — this table is what grants buyer access.
**Effect on the pending access work.** `ProjectScopeService.EnsureBuyerOwnsReservationAsync` (built in
RBAC step 5) can only compare `reservation.BuyerId` to the caller. A legitimate co-buyer has no way to
be granted access, and ownership shares cannot be recorded at all.

### B12 — `warranties` — **MAJOR**
No table. `AfterSaleClaim` has no warranty reference and no check that the unit is delivered
(`UnitId` is free, `PurchaseId` is nullable and its own comment says *"optional: tie to Purchase if you
want strict 'buyer owns this'"*).
**Doc requires.** §5.9 — claims only for `DELIVERED` units, by buyer/co-buyer, **within an active
warranty**; §6.3 — "a warranty claim must reference a delivered unit"; §5.8 — delivery starts the
warranty. Blocked behind B3/B9: there is no `DELIVERED` state to check and nothing to start a warranty.

### B13 — `work_schedules` / `schedule_exceptions` — **MAJOR**
Partial. `WeeklyAvailability` and `NotaryDateDisponibilite` exist (notary-oriented); there is no
general per-user work schedule, no exceptions table, and no half-open `[start_at, end_at)` slot model
for commercial agents.
**Doc requires.** §5.1 — "Slots come **exclusively** from internal work schedules (`work_schedules` +
`schedule_exceptions`)".

### B14 — `audit_logs`, `outbox_events`, `idempotency_keys` — **MAJOR**
None of the three exist (confirmed by DbSet list and a repo-wide search). `BusinessErrorCodes` defines
`IDEMPOTENCY_KEY_REUSED` but nothing reads an `Idempotency-Key` header.
**Doc requires.** §7 — every transition writes status history + audit log + outbox event in the same
transaction; `Idempotency-Key` mandatory on reservation submit/approve, payment creation, import
validate/commit, campaign send, final/notary appointment creation, handover completion.
**Already recorded** as outstanding in `SPEC_COMPLIANCE_AUDIT.md` §4 (§31.5/§31.6) — repeated here only
because the missing tables are a *domain* gap, not just a header gap.

### Present and conformant — no action needed
Worth stating explicitly so the migration plan doesn't disturb them:
`final_visit_cases / final_visit_appointments / final_visit_reports / snags / snag_histories`
(all with correct §5.6 three-axis separation, `PreviousAppointmentId`, `AttemptNo`, versioned reports,
and `NotaryEligibilityCalculator` implementing the §5.6 priority order server-side);
`payments` (immutable, signed amounts, `ReversalOfPaymentId`, unique external reference per source) with
`payment_schedules` / `payment_installments` / `payment_allocations`;
`unit_title_states` + `unit_title_histories` with a correct forward/backward machine;
`construction_milestones` + `construction_updates`.

---

## 7. SAV claims (§5.9)

### B15 — 5 of 10 states, no technician assignment, no gates — **MAJOR**
**Exists.** `AfterSaleClaim.cs:3` — `enum ClaimStatus { New, Read, InProgress, Resolved, Rejected }`.
Assignment is `AssignedAgentId` (an *agent*, not a technician). No SLA fields, no `reopen_count`, no
history of reopening.
**Doc requires.** §5.9 — ten states (`SUBMITTED, UNDER_REVIEW, MORE_INFO_REQUIRED, ASSIGNED,
IN_PROGRESS, WAITING_CUSTOMER, RESOLVED, REJECTED, CANCELLED, CLOSED`), the project admin qualifies and
**assigns a technician**, buyer confirmation closes the claim, reopening keeps SLA and increments a
counter, and any transition outside the matrix returns `INVALID_STATUS_TRANSITION`.
Missing `CLOSED` removes the buyer-confirmation step entirely; missing `ASSIGNED` means the
`TECHNICIAN` role (added in RBAC step 4) has no assignment concept to work against.

---

## 8. Legacy duplicate models

### B16 — `Sale` / `PaymentTracking` / `Purchase` shadow the reservation and ledger — **MAJOR**
**Exists.** `Sale` (buyer identity snapshot + `TotalPrice` + `SaleDate`), `PaymentTracking`
(`AmountPaid`, free-text `Status`), and `Purchase` — which stores `TotalPrice`, **`PaidAmount`** and
**`RemainingAmount`** as columns.
**Doc requires.** §6.3 — "Financial truth is computed from entries (validated payments − validated
reversals) — **never stored as an independent editable total**." §5.3/§5.7 — a sale is not an entity;
it is a reservation reaching `CONVERTED`.
**Effect.** Two parallel money models: the conformant `Payments`/`PaymentAllocations` ledger, and this
older stored-total path (`Sales/AddPayment/AddPaymentHandler`). They can and will disagree — the
frontend audit already found the seeded values disagreeing by 160 000 MAD (finding M3).
**Note.** The frontend's "Ventes" module was explicitly **frozen** (not migrated) by work order #2, so
nothing new depends on this path; it is contained, not spreading.

---

## 9. Commercial appointments (§5.1)

### B17 — Free-text status, no chaining, no contact — **MAJOR**
**Exists.** `Appointment.Status` is `string`; no `PreviousAppointmentId`; the person is four inline
fields (see B10). The correct shared machine (`AppointmentStateMachine`,
`AppointmentAttemptStatus` with all seven §5.1 states and the right transition table) **already exists**
in `Domain/FinalVisits/Entities/FinalVisitCase.cs` — commercial and notary appointments simply don't
use it.
**Doc requires.** §5.1 — one shared machine for commercial, final-visit and notary appointments;
terminal states end the record and a retry is a **new** appointment linked via
`previous_appointment_id`; history is never rewritten. §6.2 — `commercial_appointments(crm_contact_id,
…, previous_appointment_id self-FK)` plus `appointment_history`.

### B18 — No overlap enforcement for agents or notaries — **MAJOR**
**Exists.** Nothing. `BusinessErrorCodes.APPOINTMENT_SLOT_CONFLICT` is defined but never thrown;
no exclusion constraint, no unique index, no in-handler overlap query.
`FinalVisitAppointments` has a supporting `(StartsAt, EndsAt)` index with an ADR-0002 comment
("Supports the overlap check for an agent's slots") — the index exists, the check does not.
**Doc requires.** §5.1 and §6.3 — "no overlap per agent (enforced by domain + PostgreSQL exclusion
constraint)"; GiST exclusion constraints per agent / per notary.
**Platform note.** The database is SQL Server, which has no GiST exclusion constraints; this needs a
different mechanism (see migration step 9 and Question 3).

---

## 10. Migration plan

Ordered by dependency. **A** = additive (new table/column, no existing behaviour changes) ·
**B** = breaking (changes a column's meaning, value domain, or an endpoint's contract).
**💰 = cheap only while the database is empty** — per `RBAC_PROGRESS.md`, `GPIA_Auth` and `GPIA_Project`
hold only the 12 `AspNetRoles` rows and 7 test accounts; there is **no business data**. Every step
marked 💰 becomes a careful backfill-and-verify exercise the moment real data lands.

### Phase 1 — vocabulary and constraints (do first; entirely inside the empty-DB window)

| # | Step | A/B | 💰 | Depends on |
|---|---|---|---|---|
| 1 | **Fix `ProjectStatusCodes.Normalize`**: `"AVAILABLE"` and `"SOLD"` must **not** map to `Completed`/`Archived` — map both to `InProgress`, matching the frontend correction. Closes the unlocked final-visit gate (B6). | B | — | — |
| 2 | Convert `Unit.Status` to a `UnitCommercialStatus` enum with all 8 §3 values + CHECK constraint; default `AVAILABLE` (B3). | B | 💰 | 1 |
| 3 | Convert `Project.StatusGlobal` to the 6 canonical codes, stored (not just normalised on read); default `DRAFT` at creation (B6b, B6c). Align `Immeuble.Status` onto the same enum. | B | 💰 | 1 |
| 4 | **Create the missing filtered unique index** `IX_Reservations_ActivePerUnit` on `(UnitId)` `WHERE Status IN (0,1,4,6)`, and wrap submit/resubmit in a transaction with a row lock (B5). Mirrors the existing `IX_PaymentSchedules_ActivePerReservation`. | A | 💰 | — |
| 5 | Rename `Units.ProjectId` → `Units.ImmeubleId` and fix the XML comment (B2). | B | 💰 | — |

> Step 4 is the one that is not merely *cheaper* now but **only possible** now: a unique index cannot be
> created over data that already violates it, and by definition the violating rows are the ones a
> production system will already have accumulated.

### Phase 2 — the missing state machines (needs Phase 1's vocabulary)

| # | Step | A/B | 💰 | Depends on |
|---|---|---|---|---|
| 6 | Add `UnitStatusHistory` (append-only) and route **every** unit-status change through a command that writes it. Remove `Status` from `UpdateUnitCommand`; same for `UpdateProject`/`UpdateImmeuble` (B4, B19). | B | — | 2, 3 |
| 7 | Wire the lifecycle: submit ⇒ `HOLD_PENDING_APPROVAL`; approve ⇒ `RESERVED`; reject/expire/cancel ⇒ `AVAILABLE`; each in the same transaction as the reservation change (B4). | B | — | 6 |
| 8 | Add `NotaryAppointment.Outcome` (enum, nullable) + `PreviousAppointmentId`; move its `Status` onto the existing `AppointmentAttemptStatus` machine. Then **replace** `PUT /sold`: conversion becomes a side effect of recording `PURCHASE_COMPLETED`, setting reservation `CONVERTED` + unit `SOLD` in one transaction (B7, B8). | B | 💰 | 7 |
| 9 | Move commercial appointments onto the shared machine; add `PreviousAppointmentId` + `AppointmentHistory`; implement the per-agent/per-notary overlap check (B17, B18). | B | 💰 | — |

### Phase 3 — missing entities (largely additive; safe after Phase 2)

| # | Step | A/B | 💰 | Depends on |
|---|---|---|---|---|
| 10 | Create `CrmContacts` + `Users.CrmContactId` (unique when non-null); repoint `Reservation`, `Appointment`, `AfterSaleClaim` at it and retire the four inline identity copies (B10). | B | 💰 | — |
| 11 | Create `ReservationBuyers` (contact, ownership %, access status); derive buyer access from it in `ProjectScopeService` (B11). | A→B | 💰 | 10 |
| 12 | Create `HandoverAppointments` / `HandoverReports` / `HandoverItems` on the shared machine; report validation ⇒ unit `DELIVERED`, idempotent (B9). Retire `PropertyDelivery`. | B | 💰 | 7, 8 |
| 13 | Create `Warranties`, started by step 12; gate `AfterSaleClaim` on a delivered unit + active warranty (B12). | A | — | 12 |
| 14 | Extend `ClaimStatus` to the 10 §5.9 states; add technician assignment, SLA fields, `ReopenCount` (B15). | B | 💰 | 13 |
| 15 | Create `WorkSchedules` + `ScheduleExceptions`; source slots from them (B13). | A | — | 9 |
| 16 | Create `AuditLogs`, `OutboxEvents`, `IdempotencyKeys`; write audit + outbox inside every transition transaction; read `Idempotency-Key` on the §7 command list (B14). | A | — | 7 |
| 17 | Retire `Sale` / `PaymentTracking` / `Purchase.PaidAmount|RemainingAmount` in favour of the conformant ledger (B16). Frontend "Ventes" is frozen, so this can wait — but the stored totals should stop being *written* early. | B | 💰 | 12 |

### Phase 4 — hierarchy (largest, least urgent)

| # | Step | A/B | 💰 | Depends on |
|---|---|---|---|---|
| 18 | Introduce `Floors` between `Immeubles` and `Units`; `Unit.FloorId` replaces the building FK; project derived via floor → building → project. Villas/lots get a logical building + ground floor per §3 (B1). | B | 💰 | 5 |

Step 18 is deliberately last: it is the single most invasive change, it touches every unit query in
both APIs, and unlike steps 1–4 nothing else is blocked behind it. But it is also the step whose cost
grows fastest with data volume — if it is not done inside the empty window, it likely never gets done.

---

## 11. What the frontend cannot express, per gap

The frontend now speaks the doc's vocabulary end to end (work order #2), so each backend gap maps to a
specific thing that is *typed and ready* on the client but has nowhere to come from or go to.

| Backend gap | Frontend consequence today |
|---|---|
| **B1** no `Floor` | `Unit.floor` stays a display string. The §3 hierarchy cannot be shown or navigated; documented as a known backend gap at the `Unit` type. (Audit T2, left open.) |
| **B3 + B4** no unit machine | `UnitCommercialStatus` has all 8 states; only 3 can ever arrive, and none of them ever *change*. `HOLD_PENDING_APPROVAL` and `DELIVERED` are unreachable, so the hold step and the SAV entry condition cannot be displayed. The adapter throws on write for the 5 unmapped codes — by design, to surface exactly this. |
| **B5** no unique index | The UI cannot trust `UNIT_NOT_AVAILABLE`: `ApiExceptionFilter`'s translation branch is unreachable, so a lost double-booking race surfaces as a generic error instead of the specific one `ReservationsPage` already handles. |
| **B6** gate unlocked by `Available` | The buyer-facing final-visit request (not yet built) would be offered on unbuilt projects. `FinalVisitPanel` already defers to the API for eligibility, so it will faithfully display a wrong "eligible". |
| **B6b** no `DRAFT` | Project create/edit selects offer only `PLANNED` and `IN_PROGRESS`; a draft/unpublished project cannot be created. Commented in both selects as a backend gap. |
| **B7 + B8** no notary outcome | `NotaryAppointmentOutcome` is typed but has no wire field, so the adapter never sends it. `NotaryAppointmentsPage`'s "Marquer réalisé" completes an appointment **without recording why** — and conversion still depends on the `PUT /sold` endpoint the frontend deliberately stopped calling. Nothing can currently convert a sale correctly. |
| **B9** no handover machine | `HandoverAppointment`, `HandoverReport`, `HandoverItem` are typed; `DeliveriesPage` is display-only (its status control was removed as forbidden). Delivery cannot advance from the UI at all, and the warranty/SAV opening has no trigger. |
| **B10** no `crm_contacts` | `CrmContact` is typed but unpopulated. The §1.1 rule (a visitor becomes a contact without an account) cannot be honoured; the duplicate-alert flow has nothing to alert on. |
| **B11** no `reservation_buyers` | `ReservationBuyer` typed, `accessStatus: ACTIVE \| REVOKED` decided — but buyer access still derives from a single `BuyerId`, so the buyer portal (work order #4+) cannot scope files correctly, and co-buyers cannot be shown. |
| **B12** no warranties | `Warranty` typed; SAV claims cannot show or enforce coverage. |
| **B13** no work schedules | Appointment booking offers a free date/time input instead of real slots; "slot lists reloaded just before confirmation" (§7) is not implementable. |
| **B14** no audit/outbox/idempotency | No `Idempotency-Key` to send on submit/approve/payment; no ETag/`If-Match`, so `RESOURCE_VERSION_CONFLICT` handling in `ReservationsPage` is dead code. |
| **B15** 5 claim states | `ClaimsPage` lists only the 5 wire-representable of the 10 typed states (commented). Buyer confirmation → `CLOSED` and technician assignment cannot be built. |
| **B17 + B18** appointments | `previousAppointmentId` typed but never populated: a reschedule still overwrites history. `RESCHEDULE_PROPOSED`, `REJECTED`, `NO_SHOW` are unreachable, so `AppointmentsPage` can only advance and cancel. No slot-conflict feedback. |

---

## Questions

Points where `GPIA_WORKFLOW.md` does not settle the matter — listed rather than guessed.

1. **`Available` / `Sold` on existing project rows.** Step 1 maps both to `IN_PROGRESS` (matching the
   frontend correction). The database is empty, so nothing needs backfilling *now* — but the seed data
   and any import template will need a rule. Should a genuinely finished project be seeded directly as
   `COMPLETED`, or must it always pass through the `CompleteProject` command (which writes the audit
   entry §3 requires)? I'd recommend the command, but it makes seeding multi-step.

2. **`CONTRACTED`.** §3 shows `RESERVED → CONTRACTED → SOLD` ("final contract validated"), but no
   module workflow in §5 describes who validates that contract or when. Is `CONTRACTED` set by the
   notary flow, by a separate contract step, or is it currently unused? Nothing in §5.3 or §5.7 fills
   this in.

3. **Overlap enforcement on SQL Server.** §5.1/§6.3 specify PostgreSQL GiST exclusion constraints,
   which have no SQL Server equivalent. Options: a computed-column unique index (only prevents exact
   duplicates, not true overlap), a `CHECK`-backed trigger, or serializable-transaction enforcement in
   the handler. Is the platform choice settled, or is the doc's PostgreSQL assumption the target and
   SQL Server the temporary state?

4. **`Purchase`.** It is not in §6.2 at all, yet `ApproveReservationHandler` creates one on every
   approval and it carries the stored totals §6.3 forbids. Is `Purchase` meant to survive as the buyer
   file (§4 step 6, "buyer file appears in buyer portal"), or be retired in favour of the reservation
   + `reservation_buyers`? Step 17 assumes retirement; confirm before it is actioned.

5. **Two databases, one contact.** `crm_contacts` (B10) belongs to the CRM domain in `ProjectAPI`,
   while `users` lives in `AuthenticationAPI` with its own database. §1.1's `users.crm_contact_id`
   unique FK therefore crosses a database boundary. Should the contact live in `GPIA_Auth`, should the
   link be a soft reference validated in application code, or should the two databases be merged? This
   decision blocks step 10 and is not addressable from the doc alone.

6. **`ClaimCategory` / `ClaimPriority`.** Both are hardcoded enums. §6.1 says referentials live in
   `ref_*` tables (`code, label_fr, label_en, active`). Are these two in scope for that treatment, or
   deliberately fixed?

---

## Work order #4 — what changed

Implemented 2026-07-26. Scope was the first tranche only: **make the sale workflow enforce itself.**
`crm_contacts` (B10), `reservation_buyers` (B11), the `Floors` table (B1) and the remaining
structural findings were explicitly out of scope and are untouched.

### Step 1 — the live final-visit gate bug (B6 ✅)

`ProjectStatusCodes.Normalize` mapped legacy `"AVAILABLE"` → `Completed`, and `AllowsFinalVisit` is
the only guard on `RequestFinalVisitCommand`. Every legacy project stored as `Available` therefore
passed the gate, so a buyer could request a final visit on an unbuilt project.

`"AVAILABLE"` and `"SOLD"` now both normalise to `IN_PROGRESS` and are documented as lossy. Only the
canonical `COMPLETED` — and `DELIVERED`, which does assert the build finished — unlock the gate.
Unknown values still fail closed to `DRAFT`. The frontend carries the matching correction
(`enums.ts`, commit `dd7fbf1`).

### Step 2 — the missing concurrency backstop (B5 ✅)

`IX_Reservations_ActivePerUnit` now exists as a real filtered unique index on `(UnitId)`.

One deviation from the order's wording, deliberate: the filter is `[Status] IN (0, 1, 4, 6)`, not the
literal status names. `Reservation.Status` is persisted as `int` (`HasConversion<int>`), so the names
would not match anything. `(0, 1, 4, 6)` is the same set — SUBMITTED, APPROVED, CONVERTED,
CHANGES_REQUESTED — expressed in the stored representation, and `DRAFT` (5) stays outside it per §5.3.
A test pins the two together so they cannot drift.

The plain `IX_Reservations_UnitId` was kept alongside it: the unique index only covers active rows, so
lookups over a unit's closed reservations still need an unfiltered index.

### Step 3 — the unit commercial state machine (B3, B4, B19 ✅)

New `UnitCommercialStatus` (all 8 §3 states) + `UnitStatusCodes` (canonical UPPER_SNAKE_CASE strings)
+ `UnitStateMachine` (transition matrix, mirroring `ReservationStateMachine`). Stored as `varchar(30)`
with a `CK_Units_Status` CHECK constraint generated from the code list — §6.1 rules out database enums.

New `UnitStatusHistory` table, append-only, recording from/to, cause, reservation, actor and reason.

New `UnitStatusService` is the **single writer**. It validates the matrix and appends history, and it
deliberately does not call `SaveChanges` — the caller owns the transaction, because §3 and §5.7 require
the unit change and the reservation change to commit together.

Wired into the lifecycle, each inside an explicit transaction:

| Command | Unit transition |
|---|---|
| `CreateReservation` (submit) | `AVAILABLE → HOLD_PENDING_APPROVAL` |
| `ResubmitReservation` | idempotent `→ HOLD_PENDING_APPROVAL` (a resubmit re-asserts an existing hold) |
| `ApproveReservation` | `→ RESERVED` |
| `RejectReservation` | `→ AVAILABLE` |
| `CancelReservation` | `→ AVAILABLE` (the §3 "never automatic" admin release) |
| notary `PURCHASE_COMPLETED` | `→ SOLD`, same transaction as reservation `→ CONVERTED` |

`Status` was removed from `UpdateUnitCommand` entirely, and `UpdateUnitHandler` no longer writes it.
`InvalidUnitTransitionException` is registered in `ApiExceptionFilter` → 409 `INVALID_STATUS_TRANSITION`.

**Not wired: `SOLD → DELIVERED`.** The matrix and the service support it, but the trigger is "handover
report acknowledged", and handover reports are **B9** — explicitly deferred. The only existing
candidate, `PropertyDelivery`, is part of the legacy `Sale` model (B16) that is slated for retirement,
so wiring to it would entrench a model we are removing. Per the order's instruction to stop rather than
pull deferred work forward, this edge is left unwired and reported here.

### Step 4 — notary outcome, distinct from status (B7, B8 ✅)

New `NotaryAppointmentOutcome` enum + `NotaryOutcomeCodes` (the five §5.7 codes). `NotaryAppointment`
gains `Outcome`, `OutcomeRecordedAt`, `OutcomeRecordedBy`, `OutcomeNote`, with a
`CK_NotaryAppointments_Outcome` CHECK. Completing an appointment without an outcome is refused; an
outcome may only be recorded on a completed appointment, and it cannot be silently overwritten once set.

Only `PURCHASE_COMPLETED` does anything: reservation `→ CONVERTED` **and** unit `→ SOLD`, in one
transaction. It is idempotent — an already-converted file stays converted rather than failing.

**`PUT /api/Reservations/{id}/sold` was removed** (endpoint, command, handler, response). It converted a
reservation on request with no notarial act, no outcome and no unit transition — a second door to the
most consequential state change in the workflow, which §5.3 says has exactly one. The frontend already
stopped calling it in work order #2 (finding L3).

### Step 5 — project lifecycle (B6b ✅, B6c ⚠️)

Projects are now created in `DRAFT` (FR-CMS-001) instead of the hardcoded, misspelled `"ComingSoon"`,
and the value is normalised on write so the column only ever receives canonical codes.

`UpdateProject` now **refuses** to set `COMPLETED`: it is a gate, not a field, and may only be reached
through `CompleteProjectCommand`, which already checked 100% weighted progress, explicit confirmation
and a real end date. `SUSPENDED` is reachable through the normal update path.

B6c is only **partially** resolved: new and updated rows are canonical, but `Project.StatusGlobal` is
still a free-text column with no CHECK constraint, and `Immeuble.Status` is still parsed with
`Enum.Parse<ProjectStatus>` against a different enum. Converting those two columns the way `Unit.Status`
was converted is a natural follow-up and is still cheap while the database is empty.

### Migrations

Three, one per schema-affecting step, reviewable independently:

| Migration | Step | Contents |
|---|---|---|
| `20260726142647_AddActiveReservationPerUnitIndex` | 2 | filtered unique index (additive) |
| `20260726144023_AddUnitCommercialStatusMachine` | 3 | `Units.Status` → `varchar(30)`, legacy-value normalisation, `CK_Units_Status`, `UnitStatusHistories` table + index |
| `20260726144146_AddNotaryAppointmentOutcome` | 4 | four outcome columns + `CK_NotaryAppointments_Outcome` |

Steps 1 and 5 are behavioural only and needed no schema change.

All three were applied to `GPIA_Project` successfully. The unit-status migration includes a defensive
`UPDATE` normalising legacy PascalCase values *before* the CHECK constraint is added — a no-op on the
currently empty database, but it keeps the migration correct if replayed where data exists.

### Tests

New project `src/ProjectAPI/tests/Enforcement` (xunit + FluentAssertions), added to the solution.
**50 tests, all passing.** The pre-existing `tests/Unit` and `tests/Bdd` projects were left alone: they
are template scaffolding referencing csproj paths that do not exist, and are not in the solution.

The suite runs against **real SQL Server**, building a throwaway `GPIA_Project_EnforcementTests`
database by running the actual migrations. That is deliberate: an in-memory or SQLite provider does not
enforce filtered unique indexes or CHECK constraints, so the suite would pass while production enforced
nothing — the exact failure mode this work order exists to close.

Proving **step 2** actually enforces:
- two genuinely concurrent submits (separate contexts and connections) on one unit → exactly one wins,
  and the database ends with exactly one active reservation;
- the loser's **real** `SqlException` is fed through the **real** `ApiExceptionFilter`, asserting 409 +
  `UNIT_NOT_AVAILABLE`. This is what shows that branch is reachable at all — before this work it was
  dead code, because the index it keys on did not exist;
- the exception is confirmed to be error 2601/2627 and to name `IX_Reservations_ActivePerUnit`;
- two DRAFTs on one unit are accepted (§5.3: a draft must not block);
- a terminal reservation releases its unit for a new one;
- `UnitBlockingStatuses` is pinned to the index filter `(0, 1, 4, 6)` so they cannot drift.

Proving **step 3** actually enforces:
- the §3 matrix, allowed and forbidden transitions, including that `AVAILABLE → SOLD` and
  `RESERVED → DELIVERED` are refused and terminal states are terminal;
- a valid transition persists the status **and** writes exactly one history row with cause, actor and
  reservation;
- a refused transition leaves the unit untouched and writes **no** history;
- the idempotent variant writes no history when nothing moves;
- the database itself rejects a status outside the vocabulary, via raw SQL that bypasses the service
  entirely (`CK_Units_Status`);
- the full `AVAILABLE → HOLD_PENDING_APPROVAL → RESERVED → SOLD → DELIVERED` path leaves a complete,
  ordered audit trail.

Steps 1 and 4 are covered too (gate mapping including the legacy values and fail-closed behaviour; the
five outcome codes, rejection of unknown ones, and that the outcome and status vocabularies do not
overlap — which is the point of keeping them separate).

**One real defect was caught by these tests before it shipped:** the history row was being added
through the collection navigation only, which EF tracked as `Modified` rather than `Added`, emitting an
`UPDATE` against a non-existent row. Every reservation submit and approval would have thrown
`DbUpdateConcurrencyException` at runtime. Fixed by adding to the set explicitly.

### Still open after this tranche

`CONTRACTED` is in the enum (§3 lists it) but **no command produces it**, per the senior engineer's
ruling: the notary path goes `RESERVED → SOLD` directly, which §47.2 permits. The spec defines the
state and never assigns it an owner, a command, a screen or an endpoint. **This is an open question for
the client, not for the team.**

Unchanged and still open: B1 (Floors), B2 (`Units.ProjectId` naming), B6c (project/immeuble status
columns), B9 (handover machine — blocks `SOLD → DELIVERED`), B10 (`crm_contacts`),
B11 (`reservation_buyers`), B12 (warranties), B13 (work schedules), B14 (audit/outbox/idempotency),
B15 (claim states), B16 (legacy Sale/PaymentTracking/Purchase), B17–B18 (appointments, overlap).
