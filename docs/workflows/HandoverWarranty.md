# Workflow — Handover (livraison) and warranty

**Status: validated end to end.**

## Business purpose

After the notarial act (sale **Confirmed**), the agent plans the handover, confirms
it and writes the procès-verbal (participants, observations, keys and items).
The buyer's **acknowledgement** of that report is what flips the unit to
**DELIVERED** and starts the warranty, whose length is the one frozen on the sale
(copied from the project when the sale was created). There is exactly one delivery
flow: Handovers (`api/Handovers`). The legacy `api/deliveries` flow was removed.

## Steps

1. Schedule (`POST api/Handovers`) — only on a CONVERTED reservation with a confirmed sale.
2. Confirm (`POST <built-in function id>/confirm`).
3. Report (`POST <built-in function id>/report`) — supersedes an unacknowledged version.
4. Buyer acknowledges (`POST reports/<built-in function id>/acknowledge`) ⇒ unit DELIVERED, warranty `StartsAt = now`, `EndsAt = now + sale.WarrantyMonths`.

## Validated

Legend: ✅ validated end to end (Playwright UI test against the running stack, state re-read from the API/DB) · ⚠️ fixed during validation (fix logged in `docs/fixes/`) then validated · ❌ not validated (reason given).
Suite: `realestateFront/tests/` — run on 2026-09-14 against Azure SQL `GPIA_Project` (S2).

| Step | Result | Evidence (test) |
|---|---|---|
| 1 — handover only after the sale is confirmed | ✅ | `handover_available_only_after_sale_confirmed`, `api_rejects_status_violation` |
| 1-4 — plan, confirm, report in the UI; DELIVERED only after the buyer acknowledges | ✅ | `unit_becomes_delivered_after_handover` |
| Warranty active after delivery, length from the sale | ⚠️ fixed (buyer card hid silently on a failed read) | `warranty_active_after_delivery` |
| Warranty length set by the project admin, copied into the sale, frozen, not editable at handover | ⚠️ fixed (project edit failed on a delivered project) | `warranty_duration_set_by_project_admin_on_project`, `warranty_duration_copied_into_sale_at_creation`, `warranty_duration_frozen_after_sale_confirmed`, `warranty_duration_not_editable_by_buyer_or_agent_at_handover` |
| No second delivery flow | ⚠️ fixed (legacy `api/deliveries` + `/admin/deliveries` removed) | `no_duplicate_delivery_flow` |
| Buyer reads only own handover / warranties | ✅ | `buyer_sees_only_own_reservations_sales_visits_appointments_warranties_claims`, `buyer_pages_call_only_mine_endpoints` |
