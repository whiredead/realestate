# Fixes — Quartiers and property types (backend)

## Quartiers had no feature list
- **Files:** new `Domain/Projects/Entities/QuartierFeature.cs`, new `Infrastructure/Configurations/QuartierFeatureConfiguration.cs`, new `Application/Quartiers/Features/QuartierFeatureCommands.cs`; `Domain/Projects/Entities/Quartier.cs` (`Features`); `ApplicationDbContext.cs`; `Controllers/ProjectController.cs`; migration `20260914134245_AddQuartierFeatures`
- **Wrong:** a quartier was only name / description / images; the spec's list of features (title, description, optional image) did not exist (the per-project "amenities" are a different thing).
- **Changed:** `QuartierFeatures` table (cascade-deleted with the quartier). `GET /api/Projects/quartiers/{id}/features` (public), `POST /api/Projects/quartiers/{id}/features`, `PUT /api/Projects/quartier-features/{id}` (empty image removes it), `DELETE /api/Projects/quartier-features/{id}` (admins). Title required (≤200), description ≤2000, image optional and checked by `MediaUrlPolicy`.

## Property type: minimum area greater than maximum accepted on creation
- **Files:** `Application/TypeBiens/CreateTypeBien/CreateTypeBienHandler.cs`
- **Wrong:** only the update validator checked `MinSurface <= MaxSurface`; creation stored an impossible range.
- **Changed:** creation refuses it (422), and negative areas too.
