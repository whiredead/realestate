# Workflow — Sale (manual draft → notary confirmation)

**Status: validated end to end.**

## Business purpose

The sale records the price, the deposit already paid, the remaining amount and
the warranty granted. An agent or admin may open it as a **draft** once the
reservation is approved, the project is in delivery and the final visit is
validated. It is **never confirmed by hand**: only the notary's
`PURCHASE_COMPLETED` outcome confirms it (or creates it confirmed when no draft
exists), atomically with the reservation conversion and the unit sale.

## States

`Draft` ⇄ `PendingNotary` (follows the notary appointment being confirmed /
cancelled) → `Confirmed` (PURCHASE_COMPLETED, read-only) · `Draft`/`PendingNotary`
→ `Cancelled` (read-only; also when the reservation is cancelled).

## Validated

Legend: ✅ validated end to end (Playwright UI test against the running stack, state re-read from the API/DB) · ⚠️ fixed during validation (fix logged in `docs/fixes/`) then validated · ❌ not validated (reason given).
Suite: `realestateFront/tests/` — run on 2026-09-14 against Azure SQL `GPIA_Project` (S2).

| Rule | Result | Evidence (test) |
|---|---|---|
| "Créer une vente" only for agent / project admin / global admin | ✅ | `sale_button_visible_for_sales_agent_project_admin_global_admin`, `sale_button_hidden_for_other_roles` |
| Only when project EN_LIVRAISON, reservation approved, final visit validated, no active sale | ⚠️ fixed (eligibility endpoint + conditions list) | `sale_button_hidden_unless_project_en_livraison`, `sale_button_hidden_unless_reservation_approved`, `sale_button_hidden_unless_final_visit_validated`, `sale_button_hidden_when_active_or_finalized_sale_exists` |
| Draft content: buyer, unit location, price, deposit, remaining = price − deposit, warranty from the project | ⚠️ fixed (location fields, unit context) | `draft_sale_contains_all_required_fields`, `draft_sale_remaining_amount_equals_price_minus_reserved`, `draft_sale_inherits_warranty_from_project` |
| Draft / PendingNotary editable; Confirmed / Cancelled read-only; cancel from Draft or PendingNotary | ⚠️ fixed (PendingNotary sync) | `draft_sale_is_editable`, `pending_notary_sale_is_editable`, `confirmed_sale_is_read_only`, `cancelled_sale_is_read_only`, `sale_can_transition_to_cancelled_from_draft_and_pending_notary` |
| One active sale per reservation and per unit | ✅ | `only_one_active_sale_per_reservation`, `only_one_active_sale_per_unit` |
| Never confirmed manually; only by PURCHASE_COMPLETED; atomic | ✅ | `manual_sale_creation_never_confirms`, `sale_confirmation_only_from_purchase_completed`, `existing_draft_sale_becomes_confirmed_on_purchase_completed`, `sale_auto_created_as_confirmed_when_none_exists`, `auto_sale_creation_is_transactional` |
| Warranty copied at creation and frozen after confirmation | ✅ | `warranty_duration_copied_into_sale_at_creation`, `warranty_duration_frozen_after_sale_confirmed` |
| Project admin creates and edits drafts | ✅ | `project_admin_can_create_and_edit_draft_sale` |
| Sale refused on an unapproved reservation / finalised project | ✅ | `api_rejects_status_violation`, `finalise_makes_project_read_only` |
