# Fixes — Delivery / handover (backend)

## Duplicate legacy delivery flow
- **Files:** removed `src/ProjectAPI/src/Api/Controllers/DeliveriesController.cs`, `src/ProjectAPI/src/Api/Application/Sales/ScheduleDelivery/*` (`ScheduleDeliveryCommand/Handler`, `UpdateDeliveryStatusCommand/Handler`); `tests/Enforcement/DeliveryAssignmentConfigAndBuyerOwnershipScopeTests.cs` (the two tests of the removed handlers); comment in `UpdateNotaryAppointmentHandler.cs`
- **Wrong:** `POST api/deliveries` / `PATCH api/deliveries/{id}/status` wrote `PropertyDeliveries` rows with a free status, independently of the handover flow (`api/Handovers`: appointment → report → buyer acknowledgement → unit DELIVERED + warranty). Two sources of truth for the same event; the legacy one never delivered the unit nor started the warranty.
- **Changed:** the legacy endpoints and handlers are removed (404). Handovers is the only delivery flow. The `PropertyDeliveries` table is left in place (historical rows).
