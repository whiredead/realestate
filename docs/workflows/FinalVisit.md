# Workflow — Final visit (visite finale / réception)

**Status: validated end to end.**

## Business purpose

Before the notarial act, the buyer inspects the unit with the sales agent. The
visit yields a report (satisfactory, or unsatisfactory with reserves); a file
becomes notary-eligible only once the visit is validated and the land title is
available. It happens **after the reservation is approved and the project is in
delivery (EN_LIVRAISON), before the notary appointment** — distinct from the
handover (remise des clés), which follows the confirmed sale.

## Participants

| Layer | Component |
|---|---|
| Frontend | `features/finalVisits/FinalVisitPanel.tsx` (unit page, agent), `features/buyer/BuyerFileDetailPage.tsx` + `BuyerFinalVisitReportCard.tsx` (buyer) |
| Controller | `FinalVisitsController` (`api/final-visits/…`) |
| Handlers | request, transition (confirm / complete / no-show / cancel / reschedule), report, acknowledge, notary eligibility, case |
| Tables | `FinalVisitCases`, `FinalVisitAppointments`, `FinalVisitReports`, `Snags`, `SnagHistories`, `UnitTitleStates` |

## Steps

1. **Request** — buyer ("Demander une visite") or agent, only on an APPROVED/CONVERTED reservation of an EN_LIVRAISON project.
2. **Confirm** — agent confirms the slot (`Requested → Confirmed`).
3. **Visit** — `Completed`, or `NoShow` → the agent reschedules (new attempt, new date required) or cancels.
4. **Report** — client feedback, follow-up notes, reserves (minor / major / blocking). A major or blocking reserve makes the result unsatisfactory and requires a **motif** and a **corrective action** (422 otherwise).
5. **Acknowledge** — the buyer acknowledges the report (read-only for the buyer).
6. **Eligibility** — satisfactory report + no active blocking reserve + title available ⇒ notary appointment and sale become possible.

## Validated

Legend: ✅ validated end to end (Playwright UI test against the running stack, state re-read from the API/DB) · ⚠️ fixed during validation (fix logged in `docs/fixes/`) then validated · ❌ not validated (reason given).
Suite: `realestateFront/tests/` — run on 2026-09-14 against Azure SQL `GPIA_Project` (S2).

| Step | Result | Evidence (test) |
|---|---|---|
| 1 — request only after approval | ✅ | `final_visit_requestable_only_after_reservation_approved`, `buyer_can_request_final_visit` |
| 2-3 — confirm, complete, feedback | ⚠️ fixed (request button gated by title, feedback fields) | `agent_can_manage_visit_and_enter_feedback`, `agent_can_create_and_manage_final_visit` |
| 3 — client absent: reschedule (new date required) or cancel | ⚠️ fixed (NoShow → Cancelled allowed) | `visit_client_absent_allows_cancel_or_reschedule`, `visit_rescheduled_requires_new_date` |
| 4 — report fields persisted; unsatisfactory requires motif + corrective action | ⚠️ fixed (new report fields, 422) | `final_visit_form_persists_all_fields`, `visit_unsatisfactory_requires_reason_and_corrective_action` |
| 5 — buyer sees own visit, cannot edit feedback | ✅ | `buyer_can_view_own_visits_but_cannot_edit_feedback` |
| 6 — satisfactory visit ⇒ notary eligible | ✅ | `visit_satisfactory_makes_file_eligible_for_notary` |
| Project / building / floor / unit shown | ⚠️ fixed | `final_visit_shows_project_building_floor_unit` |
| Load failure shown instead of an endless spinner | ⚠️ fixed | `docs/fixes/ErrorHandling.md` (frontend) |
| Skipping confirmation refused at the API | ✅ | `api_enforces_state_machine_transitions` |
