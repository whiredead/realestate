# Imports

`src/ProjectAPI/src/Api/Controllers/ImportsController.cs` — class `ImportsController`, route prefix **`api/imports`**.

## Purpose

Bulk stock loading from an Excel workbook, scoped to a project the admin
picks first: download that project's example template, dry-run validate an
uploaded file, then commit a validated batch. The commit is the only path in
this module that writes to the stock tables. Import never creates a
project — only buildings and units within one the caller already has.

## Endpoints

| Verb | Path | Handler | Request | Response |
|---|---|---|---|---|
| GET | `/api/imports/projects/{projectId:guid}/template` | `GenerateImportTemplateQuery` handler | — | `.xlsx` file |
| POST | `/api/imports/projects/{projectId:guid}/validate` | `ValidateImportBatchHandler` | `IFormFile file` | `ValidateImportBatchResponse` |
| POST | `/api/imports/{batchId:guid}/commit` | `CommitImportBatchHandler` | `IFormFile file` | `CommitImportBatchResponse` |

`GET template` returns the bytes as
`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` named
`gpia-stock-import-template.xlsx`. It queries the given project's own linked
`TypeBiens` (via `Project.TypeBiens`, the same set `CreateUnit`/`UpdateUnit`
offer) and writes the names to a `VeryHidden` `Lookup` sheet, which the
example sheet's `TypeBien` column references as an in-cell dropdown
(`IXLDataValidation.List`) — a literal list would blow Excel's ~255-character
cap once the project has more than a handful of types.

Both POST actions reject a null/zero-length file or a non-`.xlsx` filename
with 400 before dispatching, read the upload into a `MemoryStream`, and pass
the byte array on the command. They take the actor from
`User.FindFirst("UserId")?.Value` — `CreatedBy` on validate, `CommittedBy` on
commit. `Commit` also reads `Request.GetIdempotencyKey()`.

## Authorization

Class-level `[Authorize(Roles = RoleGroups.Admins)]` — `GLOBAL_ADMIN` and
`PROJECT_ADMIN`. Both validate and commit call
`ProjectScopeService.EnsureProjectAccessAsync` against the route's/batch's
`projectId` — independently at each step, since the two calls don't share
state and a membership could change between them.

## Workbook format

**Each worksheet is one building.** The sheet's tab name IS the building's
name — there is no `Name` column for it. `ImportWorkbookReader.Parse`
iterates every worksheet except one reserved name, `Lookup` (the template's
own TypeBien dropdown source), and requires at least one such sheet:

> `Le fichier ne contient aucun onglet de bâtiment (chaque onglet doit représenter un bâtiment).`

Fixed layout within a building sheet (`ImportWorkbookReader.BuildingInfoRow` /
`UnitHeaderRow` / `UnitFirstDataRow`):

- **Row 1**: labels for row 2 (`Location`, `Type`, `ResidencyType`,
  `Description`, `Module3DLink`) — cosmetic, not parsed.
- **Row 2**: the building's own field values, one row.
- **Row 3**: blank, a visual separator.
- **Row 4**: the unit table's header row (`Floor`, `UnitNumber`,
  `NumberOfBedrooms`, `NumberOfBathrooms`, `ApartmentSurface`,
  `BalconySurface`, `TerraceSurface`, `GardenSurface`, `TotalSurface`,
  `View`, `Orientation`, `LatestPrice`, `TypeBien`, `SaleableValue`,
  `SaleableValue1`, `PriceSaleableValue`, `PriceSaleableValue1`).
- Column set mirrors exactly what the normal single-building/single-unit
  admin forms let someone set (`CreateImmeubleCommand`,
  `CreateProjectUnitCommand`/`UpdateUnitCommand`) — `AgentId`,
  `ImagePrincipale` and the computed stock counters on `Immeuble` are absent
  because no admin form exposes them either. The four `*SaleableValue*`
  fields are update-only on the normal Unit form (a unit is priced after
  creation, not at it); import offers them on both create and update since a
  spreadsheet row can't represent "this run is a create" vs "an update".
- No `Images` column: photos are never typed as URLs into the sheet. They're
  attached per building from its detail page (a bulk folder upload straight
  to blob storage) after the building exists — see `ImageUploadField`.
- **Row 5+**: one unit per row (`RowsUsed()` from row 5 onward — a trailing
  blank row is simply not "used" and is skipped, no explicit end-marker
  needed).

A single malformed sheet (an unreadable row 2, say) fails just that
building's row, not the whole upload; a single malformed unit row fails just
that row — both via a `try/catch` per unit that records a clear message
rather than letting one bad cell abort the whole parse.

## Validation (dry run)

`ValidateImportBatchHandler` writes an `ImportBatch` row and its per-row
results but touches no stock table. Sequence:

1. `EnsureProjectAccessAsync(request.ProjectId)`; project must exist (404).
2. Empty content ⇒ `BusinessRuleException` "Le fichier est vide."
3. Computes a SHA-256 `FileHash` over the bytes, creates the batch at
   `ImportBatchStatus.Validating` with `ProjectId`/`ProjectName` set from the
   resolved project.
4. Parses inside a `try/catch` for the ClosedXML exceptions a corrupt or
   non-`.xlsx` upload throws — recorded as a failed batch, not a bare 500. A
   `StructuralError` (no building sheets) also fails the batch. Either way the
   response carries a single synthetic row error with `Sheet = "-"`,
   `RowNumber = 0`.
5. Otherwise validates every building and unit row; `TypeBien` (when given)
   must match one of the target project's own linked types — a blank cell is
   always fine (falls back to bedroom-count matching at commit).
6. Row errors report `Sheet` as the building's own name (the sheet it came
   from), so the admin knows exactly which tab to fix.

The batch status after a clean validation is `ReadyToCommit`, which is what
the commit requires.

## Commit

`CommitImportBatchHandler`:

1. Loads the batch with its `Rows` (404 if absent);
   `EnsureProjectAccessAsync(batch.ProjectId)`.
2. Status must be `ReadyToCommit`, else 409 `INVALID_STATUS_TRANSITION`.
3. **Re-verifies the source.** SHA-256 of the bytes sent now must equal the
   stored `FileHash`, else `IMPORT_SOURCE_CHANGED`. Re-parsing a file that's
   become unreadable since validation also throws `IMPORT_SOURCE_CHANGED`
   rather than a bare 500.
4. Re-parses and requires `Buildings.Count + Units.Count == batch.TotalRows`,
   else `IMPORT_SOURCE_CHANGED` again.
5. Sets `Committing` and saves — outside the transaction, so the status is
   durable before the write begins.
6. **Transaction**: re-loads the project by id (404 → `IMPORT_SOURCE_CHANGED`
   if it was deleted since validation); inserts `Immeuble` rows with
   `Status = "SurPlan"` for each new building sheet (an existing building of
   the same name is left untouched — name match only, no destructive
   update); then, per unit row, resolves/creates its `Floor` by name within
   that building (name-matched, never duplicated across re-imports — there is
   no separate Floors tab, a floor is just a grouping the Units table
   implies) and inserts/updates the `Unit`. Sets `Completed` and saves, then
   commits.
7. On exception: rolls back, then `_db.ChangeTracker.Clear()` (not a manual
   per-entity detach loop — detaching a parent like `Floor` while a tracked
   `Unit` still points at it via a required FK throws mid-cleanup, which used
   to swallow the real error and leave the batch stuck at `Committing`
   forever). `batch` is re-attached, `Failed` is set with the exception's own
   message for known/user-safe exception types and a generic message
   otherwise, then saved and the original exception rethrown.

This is why the client must re-upload the same file to commit: the handler
re-derives the hash and the row count from the bytes rather than trusting the
stored batch.

The handler skips a unit row defensively when its building cannot be resolved
rather than throwing. `View`/`Orientation` are `NOT NULL` columns on `Unit`
but optional in the sheet — a blank cell is written as `string.Empty`, not
`null` (the literal-`null` write used to fail the insert outright).

## Dependencies

**Services:** `ApplicationDbContext`, `ProjectScopeService`,
`ImportWorkbookReader` (static), ClosedXML for workbook parsing/generation.

**Tables written:** `ImportBatches` and their row records; on commit
`Immeubles`, `Floors`, `Units`.
**Tables read:** `Projects`, `Immeubles`, `Floors`, `Units`, `TypeBiens`,
`ProjectTypeBiens`, `ImportBatches`.

## Relations

**Depends on**

| Module | Mechanism |
|---|---|
| Projects | The batch is keyed on `ProjectId`, chosen by the caller before download; the project must exist and be in the caller's perimeter. |
| Immeuble | Commit inserts `Immeuble`, `Floor` and `Unit` rows — the same tables `ImmeubleController` manages. See `docs/backend/Immeuble.md`. |
| TypeBien | The template's dropdown and validation are scoped to the project's own linked types; commit never creates a new link. |
| ProjectMembership | `ProjectScopeService` on both writes. |

**Depended on by** — nothing traced.

## Known edge cases

- The file must be uploaded twice: once to validate, once to commit. There is
  no server-side retention of the uploaded bytes, so the hash check doubles
  as the mechanism forcing the re-upload.
- `batch.Status = Committing` is saved **before** `BeginTransactionAsync`, so
  a process failure between that save and the transaction leaves a batch
  stuck at `Committing` with no rows written and no path back to
  `ReadyToCommit`.
- Leaving the template's example tab named "Exemple" and filling it in
  anyway is not refused — it just creates a building literally named
  "Exemple". There is no magic skip-by-name; the instructions on the frontend
  page are the only guard.
- Unit rows whose building cannot be resolved are skipped silently during
  commit; the response reports the batch outcome, not the skipped rows.
- `IdempotencyKey` is read from the header on commit and
  `CommitImportBatchCommand` participates in the idempotency pipeline, and
  `importsApi.commit` does generate one per call.
- Neither command has an `AbstractValidator`; the row-level rules live in the
  validate handler and the reader.

## Frontend coverage

`src/api/http/importsApi.ts` (`downloadTemplate(projectId)`,
`validate(projectId, file)`, `commit(batchId, file)`) and
`src/features/imports/ImportExcelPage.tsx`, routed at `/admin/import`
(`ADMINS`-gated, linked from `AdminLayout`'s Patrimoine section). The page
picks the project first, then walks: download → fill (duplicate the
"Exemple" tab per building, rename each copy) → upload → validate → commit.
