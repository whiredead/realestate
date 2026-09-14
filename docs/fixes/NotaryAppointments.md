# Fixes — Notary appointments

## Sales agent could not confirm an appointment
- **Files:** `src/ProjectAPI/src/Api/Controllers/NotaryAppointmentsController.cs`, `src/ProjectAPI/src/Api/Application/NotaryAppointments/UpdateNotaryAppointment/UpdateNotaryAppointmentHandler.cs`
- **Wrong:** `PUT /api/NotaryAppointments/{id}` was `AdminsNotary`; the spec makes an appointment confirmable by the agent, an admin or the notary.
- **Changed:** endpoint opened to `AdminsAgentsNotary`. A sales agent (without admin/notary role) may only set status `Confirmed` or `Cancelled`; outcome, completion and reassignment stay notary/admin (403 otherwise).
