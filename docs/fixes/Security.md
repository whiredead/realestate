# Fixes — Security (backend)

## Technicians could download reservation documents (identity papers, contracts)
- **Files:** `Application/Reservations/DownloadReservationDocument/DownloadReservationDocumentQuery.cs`
- **Wrong:** the only check was the project perimeter, so a technician or technical lead staffed on the project for after-sales work could read a buyer's CIN and signed reservation contract.
- **Changed:** internal readers are limited to GLOBAL_ADMIN, PROJECT_ADMIN, SALES_AGENT and NOTARY (still within perimeter); a buyer only their own file; anyone else 403.

## Any signed-in account could list every user's favourites
- **Files:** `Controllers/ProjectController.cs`
- **Wrong:** `GET /api/Projects/LikedProjects?UserId=…` returned the favourites (with user names) of whichever id was passed — or of everybody when none was; `Like` / `DisLikeProject` trusted the `UserId` in the body.
- **Changed:** new `GET /api/Projects/LikedProjects/mine`; for a caller without an internal role the list and both writes are pinned to the token's user.

## Buyer file page listed the global notary appointments
- **Files:** `Controllers/NotaryAppointmentsController.cs` (`GET mine`), `Notary/Appointments/GetNotaryAppointments/GetNotaryAppointmentsQuery.cs` (`MineOnly`), `GetNotaryAppointmentsHandler.cs`
- **Wrong:** the portal called the staff list endpoint and filtered in the browser (the server did narrow a buyer, but the page depended on a global endpoint).
- **Changed:** `GET /api/NotaryAppointments/mine[?ReservationId=]` returns only the caller's own appointments as buyer.

## Any signed-in account could upload, overwrite or delete catalogue files
- **Files:** `src/ProjectAPI/src/Api/Controllers/FileController.cs`
- **Wrong:** `POST api/File/upload`, `PUT update/{fileName}` and `DELETE delete/{fileName}` only required a login: a buyer could replace or delete the public images of every project.
- **Changed:** `[Authorize(Roles = AdminsAgents)]` on the three writes (the only UI callers are staff screens); download unchanged.
