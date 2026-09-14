# Workflow — After-sales claim (SAV)

**Status: validated end to end.**

## Business purpose

A buyer files a claim on their own delivered unit while its warranty is active.
The technical lead assigns a technician, who examines and resolves it, recording
the work done and before/after photos; the buyer follows progress and replies;
the technical lead validates the closure.

## State machine

`SOUMISE (Submitted)` → `ASSIGNÉE (Assigned)` | `REFUSÉE` | `ANNULÉE` ·
`Assigned` → `EN COURS D'EXAMEN (UnderReview)` | `ANNULÉE` ·
`UnderReview` → `EN RÉSOLUTION (InProgress)` | `COMPLÉMENT REQUIS` | `REFUSÉE` ·
`MoreInfoRequired` → `UnderReview` | `ANNULÉE` ·
`InProgress` → `EN ATTENTE CLIENT` | `RÉSOLUE (Resolved)` · `WaitingCustomer` → `InProgress` ·
`Resolved` → `InProgress` (reopened) | `VALIDÉE / FERMÉE (Closed)`.
Supervisors (global admin, project admin, tech lead, within perimeter) assign,
refuse and validate; the assigned technician works the claim.

## Validated

Legend: ✅ validated end to end (Playwright UI test against the running stack, state re-read from the API/DB) · ⚠️ fixed during validation (fix logged in `docs/fixes/`) then validated · ❌ not validated (reason given).
Suite: `realestateFront/tests/` — run on 2026-09-14 against Azure SQL `GPIA_Project` (S2).

| Step | Result | Evidence (test) |
|---|---|---|
| Creation requires a confirmed sale, a delivered unit, an active warranty | ⚠️ fixed (ownership via confirmed sale) | `claim_requires_confirmed_sale`, `claim_requires_delivered_unit`, `claim_requires_active_warranty`, `claim_blocked_after_warranty_expiry`, `buyer_can_create_claim_only_within_warranty_period` |
| Buyer cascade Projet → Immeuble → Étage → Unité → Vente, own units only | ⚠️ fixed (eligible-units endpoint + cascade) | `claim_cascade_shows_only_own_project_building_floor_unit_sale` |
| Description, category, priority, attachments persisted | ⚠️ fixed | `claim_form_persists_description_category_priority_attachments` |
| Workflow order SOUMISE → … → VALIDÉE/FERMÉE; skipped steps refused | ⚠️ fixed (state machine re-ordered to the spec) | `claim_workflow_follows_state_machine`, `claim_invalid_transition_rejected`, `api_enforces_state_machine_transitions` |
| Tech lead views, assigns, validates; technician cannot validate | ⚠️ fixed (supervisor / technician split, TECH_LEAD role) | `tech_lead_can_view_assign_and_validate`, `tech_lead_can_assign_claim_to_technician`, `tech_lead_can_validate_claim_closure`, `technician_cannot_validate_closure`, `api_rejects_role_violation` |
| Technician sees only assigned claims; work-done messages; before / after photos | ⚠️ fixed (detail, comments, photo phase, access policy) | `technician_sees_only_assigned_claims`, `technician_can_add_work_done_comments_and_before_after_photos` |
| Buyer follows history and replies | ⚠️ fixed | `buyer_can_follow_progress_and_reply` |
| Project / building / floor / unit shown | ⚠️ fixed | `claim_detail_shows_project_building_floor_unit` |
| Evidence readable only by owner, assigned technician, supervisors | ⚠️ fixed | `api_upload_requires_token_role_ownership_and_project_scope`, `api_rejects_file_access_from_non_owner` |
| SLA detection job (`SavSlaDetectionJob`) | ❌ | time-based background job, not driven by the UI suite |
