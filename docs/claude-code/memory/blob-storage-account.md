---
name: blob-storage-account
description: "GPIA's live Azure Blob account is stgpia1539; docs reference a stale blobgpia that no longer resolves"
metadata: 
  node_type: memory
  type: project
  originSessionId: d889e0f5-3bbe-40e9-8a14-c4d12e449f06
  modified: 2026-09-12T14:36:37.850Z
---

The project's live Azure Storage account is **stgpia1539** (`https://stgpia1539.blob.core.windows.net/`). It has one container, `images`, with public access level `Blob`, holding ~39 manually-uploaded demo assets (apartment-*.jpg, immeuble-*.jpg) dated 2026-08-31. No `documents` container exists yet, though `UploadReservationDocumentHandler.ContainerName` targets one.

The older account named in `ProjectAPI_Fix_Checklist.md` and `docs/GAP_ANALYSIS.md`, **blobgpia**, is dead — its hostname no longer resolves in DNS. Ignore those URLs.

The connection string lives only in .NET user-secrets for `ProjectAPI.Api` (UserSecretsId `fc785812-cac4-407f-8c87-64b7e1f923a1`), never in appsettings.json — `BlobStorage:ConnectionString` is deliberately empty there. Note that empty string is not null, so Program.cs's `?? throw` does not catch a missing config; it falls through to `new BlobServiceClient("")`, which throws at startup.

**Why:** the account name appears nowhere in either repo, so it can't be recovered from the code, and the docs point at the wrong (dead) account.

**How to apply:** when blob uploads fail locally, check user-secrets first, not appsettings. See [[local-dev-environment]].
