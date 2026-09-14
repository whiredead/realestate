# Workflow — Project lifecycle (SUR_PLAN → EN_LIVRAISON → FINALISE)

**Status: validated end to end.**

## Business purpose

A project is sold off-plan (**SUR_PLAN**: reservations allowed), moves to
delivery once construction reaches 100 % of the weighted milestones
(**EN_LIVRAISON**: no new reservation, but approved files continue — final visit,
sale, notary, handover), and is finally closed by the project admin
(**FINALISE**: read-only). Construction progress is never typed in: it is
computed from milestone weights.

## Steps

1. Create the project (identity, status, warranty, images, 3D link, videos, quartier, property types) — admins.
2. Milestones started / completed ⇒ weighted progress.
3. "Passer en livraison" (`POST construction/projects/<built-in function id>/complete`) — 100 % required (a global-admin derogation with reason exists).
4. "Finaliser le projet" (`POST …/finalize`) — from EN_LIVRAISON, project admin / global admin.
5. FINALISE ⇒ every business write on the project is refused (409 `PROJECT_READ_ONLY`, save-time guard); after-sales claims and warranties continue.

## Validated

Legend: ✅ validated end to end (Playwright UI test against the running stack, state re-read from the API/DB) · ⚠️ fixed during validation (fix logged in `docs/fixes/`) then validated · ❌ not validated (reason given).
Suite: `realestateFront/tests/` — run on 2026-09-14 against Azure SQL `GPIA_Project` (S2).

| Step | Result | Evidence (test) |
|---|---|---|
| 1 — form persists every field, several images, several property types, quartier, video | ⚠️ fixed (type biens / quartier / videos in the form, list crash on null interior images, id lookup) | `project_form_persists_all_fields`, `project_form_accepts_multiple_images`, `project_form_accepts_multiple_property_types`, `project_can_link_one_or_many_property_types` |
| Only admins create / edit | ✅ | `agent_cannot_create_project`, `agent_cannot_edit_project`, `api_rejects_role_violation` |
| 2 — weighted progress from milestones | ✅ | `project_form_persists_all_fields` |
| SUR_PLAN allows reservations; EN_LIVRAISON blocks new ones but files continue | ✅ | `sur_plan_allows_reservation_creation`, `en_livraison_blocks_new_reservation` |
| 3 — refused below 100 % | ✅ | `project_admin_finalize_rejected_below_100_pct`, `finalise_transition_rejected_below_100_pct` |
| 4 — two-step finalisation by the project admin only | ⚠️ fixed (two steps, finalize command) | `project_admin_can_finalize_project_at_100_pct`, `finalise_transition_only_by_project_admin` |
| 5 — FINALISE read-only (UI and API) | ⚠️ fixed (the read-only rule was never enforced) | `finalise_makes_project_read_only`, `forbidden_actions_hidden_or_disabled_by_role_and_status` |
| Dashboard: reservations, amounts, rate, average, construction progress, units by state; updates after a reservation | ⚠️ fixed (new metrics + project filter) | `dashboard_shows_all_required_metrics`, `dashboard_metrics_update_after_new_reservation` |
| Deletion of a project without history | ⚠️ fixed | see `ProjectDeletionCascade.md` |
