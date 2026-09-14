# Imports

`src/ProjectAPI/src/Api/Controllers/ImportsController.cs` — class `ImportsController`, route prefix **`api/imports`**.

## Purpose

Bulk stock loading from an Excel workbook: download a template, dry-run validate
an uploaded file, then commit a validated batch. The commit is the only path in
this module that writes to the stock tables.

## Endpoints

| Verb | Path | Handler | Request | Response |
|---|---|---|---|---|
| GET | `/api/imports/template` | `GenerateImportTemplateQuery` handler | — | `.xlsx` file |
| POST | `/api/imports/projects/{projectId:guid}/validate` | `ValidateImportBatchHandler` | `IFormFile file` | `ValidateImportBatchResponse` |
| POST | `/api/imports/{batchId:guid}/commit` | `CommitImportBatchHandler` | `IFormFile file` | commit response |

`GET template` returns the bytes as
`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` named
`gpia-stock-import-template.xlsx`.

Both POST actions reject a null or zero-length file with 400 before dispatching,
read the upload into a `MemoryStream`, and pass the byte array on the command.
They take the actor from `User.FindFirst("UserId")?.Value` —
`CreatedBy` on validate, `CommittedBy` on commit. `Commit` also reads
`Request.GetIdempotencyKey()`.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.Admins)]` — `GLOBAL_ADMIN` and
`PROJECT_ADMIN`. Both the validate and commit handlers call
`ProjectScopeService.EnsureProjectAccessAsync`; validate scopes on the route's
`projectId`, commit on the stored `batch.ProjectId`.

## Workbook format

`ImportWorkbookReader.Parse` requires two worksheets named **`Buildings`** and
**`Units`**, matched case-insensitively. A file missing either produces a
`StructuralError`:

> `Le fichier doit contenir les onglets 'Buildings' et 'Units'.`

The first row of each sheet is skipped as a header (`RowsUsed().Skip(1)`), and
the remainder parse into `BuildingRow` and `UnitRow` DTOs.

## Validation (dry run)

`ValidateImportBatchHandler` writes an `ImportBatch` row and its per-row results
but touches no stock table. Sequence:

1. `EnsureProjectAccessAsync(request.ProjectId)`; project must exist (404).
2. Empty content ⇒ `BusinessRuleException` "Le fichier est vide."
3. Computes a SHA-256 `FileHash` over the bytes, creates the batch at
   `ImportBatchStatus.Validating`.
4. Parses. A parse failure sets `Failed`; a `StructuralError` sets
   `ValidationFailed`. Either way the response carries a single synthetic row
   error with `Sheet = "-"`, `RowNumber = 0`.
5. Otherwise it validates each building and unit row and collects
   `RowErrorDto { Sheet, RowNumber, Message }` entries.

The batch status after a clean validation is `ReadyToCommit`, which is what the
commit requires.

## Commit

`CommitImportBatchHandler`:

1. Loads the batch with its `Rows` (404 if absent);
   `EnsureProjectAccessAsync(batch.ProjectId)`.
2. Status must be `ReadyToCommit`, else 409 `INVALID_STATUS_TRANSITION`.
3. **Re-verifies the source.** SHA-256 of the bytes sent now must equal the
   stored `FileHash`, else `IMPORT_SOURCE_CHANGED`.
4. Re-parses and requires `Buildings.Count + Units.Count == batch.TotalRows`,
   else `IMPORT_SOURCE_CHANGED` again.
5. Sets `Committing` and saves — outside the transaction, so the status is
   durable before the write begins.
6. **Transaction**: inserts `Immeuble` rows with `Status = "ComingSoon"`, saves,
   then inserts `Floor` rows and `Unit` rows with
   `Status = UnitCommercialStatus.Available`. Sets `Completed` and saves, then
   commits.
7. On exception the catch sets `Failed`, saves, and rethrows.

This is why the client must re-upload the same file to commit: the handler
re-derives the hash and the row count from the bytes rather than trusting the
stored batch.

Units are created `Available` and buildings `"ComingSoon"` — the same literal
`CreateImmeubleHandler` writes, without passing through
`ProjectStatusCodes.Normalize`.

The handler skips a unit row defensively when its building cannot be resolved
rather than throwing.

## Dependencies

**Services:** `ApplicationDbContext`, `ProjectScopeService`,
`ImportWorkbookReader` (static), ClosedXML for workbook parsing.

**Tables written:** `ImportBatches` and their row records; on commit
`Immeubles`, `Floors`, `Units`.
**Tables read:** `Projects`, `Immeubles`, `Floors`, `Units`, `ImportBatches`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| Projects | The batch is keyed on `ProjectId`; the project must exist. |
| Immeuble | Commit inserts `Immeuble`, `Floor` and `Unit` rows — the same tables `ImmeubleController` manages. See `docs/backend/Immeuble.md`. |
| ProjectMembership | `ProjectScopeService` on both writes. |

**Depended on by** — nothing traced.

## Known edge cases

- The file must be uploaded twice: once to validate, once to commit. There is no
  server-side retention of the uploaded bytes, so the hash check in step 3
  doubles as the mechanism forcing the re-upload.
- `batch.Status = Committing` is saved **before** `BeginTransactionAsync`, so a
  process failure between that save and the transaction leaves a batch stuck at
  `Committing` with no rows written and no path back to `ReadyToCommit`.
- Commit inserts `Immeuble` rows with the legacy status literal `"ComingSoon"`
  rather than a normalised code.
- Unit rows whose building cannot be resolved are skipped silently during commit;
  the response reports the batch outcome, not the skipped rows.
- `IdempotencyKey` is read from the header on commit and `CommitImportBatchCommand`
  participates in the idempotency pipeline, but the frontend sends no such header
  anywhere.
- Both POST actions return 400 with a plain-text French message for a missing
  file, rather than a `problem+json` body.
- Neither command has an `AbstractValidator`; the row-level rules live in the
  validate handler and the reader.

## Frontend coverage

**No frontend caller.** A search for `/api/imports` across
`realestateFront/src/` returns no matches, and there is no adapter file for this
controller.

All three endpoints are reachable only through direct API access.
