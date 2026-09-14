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

## Project list crashed (500) for every caller once a linked type had no interior images
- **Files:** `src/ProjectAPI/src/Api/Application/Projects/GetAllProjects/GetAllProjectsHandler.cs`
- **Wrong:** the mapping did `tb.ImagesInterieur!.Split(',')`. `ImagesInterieur` is null for most property types, so as soon as one project in the page was linked to such a type, `GET /api/Projects` threw a NullReferenceException for everybody — the projects page, the dashboard project filter and the reservation form all lost their project list.
- **Changed:** null/blank → empty list; entries trimmed.

## Project detail looked a project up by scanning a 1000-row page
- **Files:** `GetAllProjects/GetAllProjectsQuery.cs`, `GetAllProjectsHandler.cs`, `Domain/Projects/Interfaces/IProjectRepository.cs`, `Infrastructure/Repositories/ProjectRepository.cs`
- **Wrong:** there is no by-id read returning the full editable project, so the client listed `PageSize=1000` and searched it; the server caps a page at 500, so past 500 projects the detail page said "Projet introuvable".
- **Changed:** `GET /api/Projects?Id={id}` filters on the id server-side (still inside the caller's perimeter).

## Property types could not be linked from the project form; unknown quartier → 500
- **Files:** new `Application/Projects/ProjectTypeBienLinks.cs`; `CreateProjects/CreateProjectCommand.cs`, `CreateProjectHandler.cs`, `UpdateProjects/UpdateProjectCommand.cs`, `UpdateProjectHandler.cs`
- **Wrong:** Project ↔ TypeBien links existed only as a one-by-one `POST {id}/type-biens` the back office never called, with no way to unlink. A `QuartierId` that does not exist failed on the foreign key.
- **Changed:** `TypeBienIds` on create and update sets the project's links as a whole list (added / removed to match; unknown ids → 422). `QuartierId` is validated (unknown → 422).

## A finalised project was not read-only
- **Files:** new `Domain/Construction/Entities/ProjectReadOnlyException.cs`, new `Infrastructure/Context/ProjectReadOnlyGuard.cs`; `Infrastructure/Context/ApplicationDbContext.cs` (`SaveChanges*` overrides); `Api/Filters/ApiExceptionFilter.cs`; `BusinessErrorCodes.ProjectReadOnly`
- **Wrong:** `ProjectStatusCodes.IsReadOnly` (FINALISE / SUSPENDED) existed but no code called it: a finalised project could still be edited, get new buildings, floors, units, milestones, document rules, reservations, final visits, notary appointments and handovers through any handler that only checked its own precondition.
- **Changed:** every save resolves the project of each added/modified/deleted business row (project, building, floor, unit, milestone, construction update, document requirement, feature, type link, reservation and its documents, sale, notary appointment, handover, final-visit case/appointment/report) and refuses the whole save with **409 `PROJECT_READ_ONLY`** when that project is stored as FINALISE/SUSPENDED. The finalisation command itself passes (the stored status is checked). After-sales claims, warranties, memberships and notifications stay writable — the warranty outlives the project.

## Quartier list returned only id and name
- **Files:** `Application/Quartiers/GetQuartiers/QuartierListItem.cs`, `GetQuartiersHandler.cs`
- **Wrong:** the referential screen's "Description" column was always "-".
- **Changed:** `Description` and `Images` included.

## No project could be deleted; deletion loaded whole tables and was not atomic
- **Files:** `src/ProjectAPI/src/Api/Application/Projects/RemoveProject/RemoveProjectHandler.cs`; `tests/Enforcement/ProjectMutationAndDashboardScopeTests.cs`
- **Wrong:** the handler loaded every building, unit, reservation, sale and appointment of the database into memory, deleted units/buildings one save at a time (no transaction), and failed on the first configuration row still referencing the project (memberships, milestones, document rules, videos, type links, leads…) — every real project answered 409 RESOURCE_IN_USE.
- **Changed:** existence checks in SQL; refused (409 `RESOURCE_IN_USE`) when the project has reservations, sales, deliveries, commercial appointments or feedback, and (409 `PROJECT_READ_ONLY`) when finalised; otherwise the project and all its configuration (units and their histories/title states, floors, building features/plans/tracking/type links/assignments, buildings, construction milestones/updates, videos, invitation assignments, leads, likes, agent assignment config, project assignments, features, type links, amenities, document rules, memberships) are deleted in one transaction.

## Building deletion erased reservations and sales
- **Files:** `src/ProjectAPI/src/Api/Application/Immeubles/DeleteImmeuble/DeleteImmeublesHandler.cs`; `tests/Enforcement/InventoryScopeTests.cs`
- **Wrong:** deleting a building first deleted its **sales and reservations** (commercial records), non-transactionally, after loading every unit/sale/reservation into memory — and still failed on configuration rows.
- **Changed:** same model as project deletion: refused when the building has business history or its project is finalised; otherwise building, floors, units and their configuration rows deleted in one transaction.
