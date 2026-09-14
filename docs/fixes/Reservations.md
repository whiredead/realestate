# Fixes — Reservations & documents (ProjectAPI)

## Approval ignored the required documents
- **Files:** `src/ProjectAPI/src/Api/Application/Reservations/ApproveReservation/ApproveReservationHandler.cs`
- **Wrong:** only creation/submission checked the project checklist; a file whose piece was removed, or submitted before the project added a requirement, could be approved.
- **Changed:** approval calls `ReservationDocumentChecklist.EnsureSubmittableAsync` → 422 `MISSING_REQUIRED_DOCUMENT`.

## No default checklist
- **Files:** `Application/Projects/CreateProjects/CreateProjectHandler.cs`
- **Wrong:** a new project required no document, so any file could be submitted bare.
- **Changed:** every new project is seeded with `CIN` (CIN ou passeport), `RESERVATION_CONTRACT` (contrat ou bulletin signé), `RESERVATION_PAYMENT_PROOF` (justificatif de paiement), all required and editable per project.

## Documents could be deleted at any status; not-found errors were 400/500
- **Files:** `Application/Common/Reservations/ReservationDocumentPolicy.cs` (new), `Reservations/UploadReservationDocument/UploadReservationDocumentHandler.cs`, `Reservations/DeleteReservationDocument/DeleteReservationDocumentHandler.cs`, `Controllers/ReservationsController.cs`
- **Wrong:** a piece could be removed from a submitted or approved file (the documents the decision relied on); unknown reservation on upload threw `KeyNotFoundException` (unmapped → 500); unknown document on delete → 400 text; empty upload → 400 text.
- **Changed:** add allowed unless REJECTED/CANCELLED/EXPIRED; delete only in DRAFT/CHANGES_REQUESTED (409 otherwise); unknown ids → 404; empty file → 422 with a `File` field error.

## Reservation list: status filtered client-side, no building filter
- **Files:** `Reservations/GetReservations/GetReservationsQuery.cs`, `GetReservationsHandler.cs`
- **Wrong:** no `Status` parameter, so the console filtered one page on the client (matching rows on other pages were hidden); no way to filter by building.
- **Changed:** `Status` and `ImmeubleId` query parameters, applied before paging.

## Historical 403 on document upload
- Not reproducible with an in-scope agent (200). A 403 is still returned — correctly — to an agent without a membership on the reservation's project (`PROJECT_SCOPE_DENIED`). Covered by `document_upload_does_not_return_403`.
