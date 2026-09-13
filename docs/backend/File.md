# File

`src/ProjectAPI/src/Api/Controllers/FileController.cs` — class `FileController`, route prefix **`api/File`**.

## Purpose

Generic blob-storage operations against the **default container** configured in
`BlobStorageSettings.ContainerName`. Upload, delete and update go through
MediatR handlers; download calls `IBlobStorageService` directly from the
controller.

This is the only file API that is not tied to a business entity. Reservation
documents use their own endpoints and a different container — see
`docs/backend/Reservations.md`.

## Endpoints

| Verb | Path | Handler | Request | Response |
|---|---|---|---|---|
| POST | `/api/File/upload` | `UploadFilesHandler` | `UploadFilesCommand` (multipart, `Files`) | `UploadFilesResponse` |
| DELETE | `/api/File/delete/{fileName}` | `DeleteFileHandler` | route `fileName` | plain-text message |
| PUT | `/api/File/update/{fileName}` | `UpdateFileHandler` | `UpdateFileCommand` (multipart, `File`) | plain-text link or message |
| GET | `/api/File/download/{fileName}` | *(none — direct service call)* | route `fileName` | file stream or 404 |

## Authorization

Class-level `[Authorize]`. No role attributes and no per-file ownership check on
any action. Any authenticated caller may upload, download, update or delete any
file in the default container by name.

## Behaviour

**Upload** — the controller returns 400 when `command.Files` is null or empty.
The handler skips null or zero-length entries, names each blob
`{Guid}_{Path.GetFileName(original)}`, uploads with the browser-supplied
`ContentType` (defaulting to `application/octet-stream`), and returns the list of
resulting URLs. Files that were skipped produce no entry and no error, so the
returned list can be shorter than the input.

**Delete** — calls `container.DeleteAsync(fileName)` and maps the boolean to
`Ok(message)` or `BadRequest(message)`. A missing file and a failed delete are
reported identically, as 400 with `"File not found or couldn't be deleted."`

**Update** — deletes the existing blob first. If the delete returns false the
handler stops and reports failure. Otherwise it uploads the new file under a
**new** name (`{Guid}_{original}`) and returns that link. The old name is not
reused, so the URL changes and any stored reference to the previous link becomes
stale.

**Download** — bypasses MediatR:

```csharp
var (content, contentType, exists) = await _blobStorageService.ForContainer()
    .DownloadAsync(fileName, HttpContext.RequestAborted);
if (!exists || content == null) return NotFound(...);
return File(content, contentType ?? "application/octet-stream", fileName);
```

The `fileName` route value is passed to the storage service unchanged.

## Dependencies

**Services:** `IBlobStorageService` (injected into the controller and into all
three handlers), `IMediator`.

`IBlobStorageService.ForContainer()` with no argument resolves the default
container from `BlobStorageSettings`. Handlers that need a different container —
`UploadReservationDocumentHandler` and `DeleteReservationDocumentHandler` — pass
a name explicitly.

**Tables:** none. This controller writes no database rows; uploaded files are
not recorded anywhere. The association between a blob URL and a business entity
is made by whichever caller stores the returned link.

## Relations

**Depended on by** — the frontend's image and document pickers. No backend module
dispatches `UploadFilesCommand`, `DeleteFileCommand` or `UpdateFileCommand`.

**Related** — `docs/backend/Reservations.md` covers the document upload path that
writes `ReservationDocument` rows and targets the `documents` container. The two
do not share handlers.

## Known edge cases

- No ownership or scoping of any kind. Any authenticated user can download or
  delete any blob in the default container given its name, including files
  uploaded by other users for other projects.
- `fileName` is taken from the route and passed to the storage SDK without
  validation or normalisation.
- Upload returns 200 with a possibly shorter list than the input when entries are
  null or zero-length; the caller cannot tell which inputs were skipped.
- `Update` performs delete-then-upload with no transaction: if the upload fails
  after a successful delete, the original file is gone and the response reports
  failure only for the upload.
- `Update` changes the blob name, so the returned link differs from the one
  addressed in the route.
- Delete and update return plain-text bodies (`Ok(response.Message)`,
  `Ok(response.FileLink)`), unlike the JSON responses elsewhere in the API.
- Nothing records what was uploaded, so there is no server-side way to enumerate
  or audit files.

## Frontend coverage

`realestateFront/src/api/http/filesApi.ts`; used by
`components/ui/ImageUploadField.tsx`.

| Endpoint | Frontend caller |
|---|---|
| POST `upload` | `filesApi.upload` |
| DELETE `delete/{fileName}` | `filesApi.remove` |
| GET `download/{fileName}` | `filesApi.downloadUrl` (builds a URL string only) |
| PUT `update/{fileName}` | **no caller** |

Three of the four endpoints are used. `filesApi` does not go through
`projectFetch`; it calls `fetch` directly so it can:

- omit `Content-Type` and let the browser set the multipart boundary;
- append files under the field name `Files`, matching
  `UploadFilesCommand.Files`;
- apply a 30-second `AbortController` timeout;
- return a typed `UploadResult` discriminated union instead of throwing, mapping
  401/403 to `unauthorized`, 400 to `invalid`, and anything else — including an
  abort or a network failure — to `storage_unavailable`.

It also treats a 200 response carrying an empty `fileLinks` list as a failure
when files were submitted.

`filesApi.downloadUrl` returns
`{projectApiUrl}/api/File/download/{encoded name}` as a plain string. Because
that URL is used directly by the browser (for example as an `img` or link
target), it carries no `Authorization` header, while the endpoint requires
authentication.
