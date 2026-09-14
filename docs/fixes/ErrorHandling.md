# Fixes — Error propagation (ProjectAPI)

## Refused access returned as HTTP 500
- **Files:** `src/ProjectAPI/src/Api/Controllers/SalesController.cs` (`GET /api/sales/user/{userId}`)
- **Wrong:** a `catch (Exception)` wrapped every error in a 500 `InternalServerError` — a buyer asking for another buyer's sales got a 500 whose message said "Vous n'avez pas accès à ce dossier".
- **Changed:** local catch removed; `BuyerScopeDenied` now reaches `ApiExceptionFilter` → 403 with a stable code.

## Immeuble list turned any error into a raw 400 string
- **Files:** `Controllers/ImmeubleController.cs` (`GET /api/Immeuble`)
- **Changed:** local catch removed; errors are typed problem responses from the filter.

## Delete handlers leaked exceptions/stack traces as 400, not-found as 400
- **Files:** `Application/Projects/RemoveProject/RemoveProjectHandler.cs`, `Application/Immeubles/DeleteImmeuble/DeleteImmeublesHandler.cs`, `Application/TypeBiens/DeleteTypeBien/DeleteTypeBienHandler.cs`
- **Wrong:** unknown id → 400 "operation failed"; unexpected exceptions returned as 400 with `ex.ToString()` (full stack trace) in the body; type-de-bien "still associated" → 400 with a custom body the frontend could not read (`detail` missing).
- **Changed:** not found → `NotFoundException` (404); in-use → `BusinessRuleException` 409 `RESOURCE_IN_USE` with a French message; other exceptions propagate to the filter (500 + requestId, no stack trace).

## Foreign-key violations surfaced as 500
- **Files:** `Filters/ApiExceptionFilter.cs`, `Application/Common/Exceptions/BusinessErrorCodes.cs`
- **Wrong:** only unique-index violations (2601/2627) were translated; deleting a row still referenced (SQL 547) produced a generic 500.
- **Changed:** SQL 547 anywhere in the exception chain → 409 `RESOURCE_IN_USE` ("Cet élément est encore utilisé par d'autres données…"). Frontend `BusinessErrorCode` union gained `RESOURCE_IN_USE`.

## Transient database failures surfaced as a bare 500
- **Files:** `Api/Filters/ApiExceptionFilter.cs`, `Application/Common/Exceptions/BusinessErrorCodes.cs` (`SERVICE_UNAVAILABLE`)
- **Wrong:** a command timeout or a dropped connection to Azure SQL (Basic tier throttling, failover) returned `500 INTERNAL_ERROR`, indistinguishable from a bug.
- **Changed:** SqlException numbers -2, -1, 2, 53, 4060, 4221, 10053, 10054, 10060, 10928, 10929, 40197, 40501, 40613, 49918-49920 → **503 `SERVICE_UNAVAILABLE`** with `Retry-After: 5` and a "réessayez" message.

## Write on a finalised project
- **Files:** `Api/Filters/ApiExceptionFilter.cs`
- **Changed:** `ProjectReadOnlyException` → **409 `PROJECT_READ_ONLY`** (see Projects.md).
