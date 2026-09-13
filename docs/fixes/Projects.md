# Fixes — Projects, quartiers, construction

## No way to reach FINALISE
- **Files:** `src/ProjectAPI/src/Api/Application/Construction/FinalizeProject/FinalizeProjectCommand.cs` (new), `src/ProjectAPI/src/Api/Controllers/ConstructionController.cs`, `Construction/CompleteProject/CompleteProjectCommand.cs`
- **Wrong:** the business phase FINALISE maps to `ARCHIVED`, but no command wrote `ARCHIVED`; `complete` could also be re-run on an archived project.
- **Changed:** two-step lifecycle. `POST /api/construction/projects/{id}/complete` (100 % → COMPLETED = EN_LIVRAISON), then new `POST /api/construction/projects/{id}/finalize` (Admins, in scope, explicit confirmation, phase must be EN_LIVRAISON, weighted progress 100 % unless GLOBAL_ADMIN derogation with reason) → ARCHIVED, audited as a ConstructionUpdate. `complete` refuses COMPLETED **and** ARCHIVED.

## Project edit could bypass the lifecycle gates
- **Files:** `Projects/UpdateProjects/UpdateProjectHandler.cs`
- **Wrong:** only a move *to* COMPLETED was blocked. A PUT could write ARCHIVED directly, or move a COMPLETED/ARCHIVED project back to IN_PROGRESS (reopening reservations).
- **Changed:** a status change to ARCHIVED, or away from COMPLETED/ARCHIVED, is refused (409).

## Project admin locked out of the project they created
- **Files:** `Projects/CreateProjects/CreateProjectHandler.cs`
- **Wrong:** a PROJECT_ADMIN could create a project, but no membership was written, so every later action on it failed the §6.4 perimeter check.
- **Changed:** when the creator is a PROJECT_ADMIN (not GLOBAL_ADMIN), an active PROJECT_ADMIN membership is created for them.

## Project deletion cascaded through business history
- **Files:** `Projects/RemoveProject/RemoveProjectHandler.cs`
- **Wrong:** DELETE hard-deleted reservations and sales along with the project.
- **Changed:** refused (409) when any unit of the project carries a reservation or a sale; only a project with no commercial history can be deleted.

## Quartier could not be deleted
- **Files:** `Quartiers/DeleteQuartier/DeleteQuartierCommand.cs` (new), `Controllers/ProjectController.cs`
- **Wrong:** CRUD on quartiers was missing the D.
- **Changed:** `DELETE /api/Projects/quartiers/{id}` (Admins); refused (409) while a project references the quartier.

## Property type update rejected every call from the admin screen
- **Files:** `src/ProjectAPI/src/Api/Controllers/TypeBiensController.cs`, `Application/TypeBiens/UpdateTypeBien/UpdateTypeBienHandler.cs`
- **Wrong:** `PUT /api/TypeBiens/{id}` returned 400 unless the body repeated the id; unknown id → 400 with a non-problem body.
- **Changed:** id taken from the route; unknown id → 404 `NotFoundException`; controller no longer maps `IsSuccess=false` to an unreadable 400.
