---
name: local-dev-environment
description: Committed appsettings point at LocalDB (the SQLEXPRESS switch was reverted); machine-specific sqlcmd/PowerShell encoding traps
metadata: 
  node_type: memory
  type: project
  originSessionId: ea59dcf3-33cf-4b1c-8c52-f7b32070feec
  modified: 2026-09-13T16:42:01.995Z
---

Verified 2026-09-12: both committed `appsettings.json` files use
`Server=(localdb)\MSSQLLocalDB` (`GPIA_Auth` for AuthenticationAPI port 48988,
`GPIA_Project` for ProjectAPI port 48989). The string `SQLEXPRESS` appears
nowhere in the backend repo, and there are no `appsettings.Development.json`
overrides — so the 2026-07-24 switch to `DESKTOP-1CEDKH6\SQLEXPRESS` recorded
here previously was reverted or never committed. Trust the appsettings, not
this note, for which instance is live.

Databases were rebuilt at some point with `dotnet ef database update`
(`InitialCreate` per project's Infrastructure) and held no business data
(0 projects, 0 units, 0 `ProjectAssignments`) — only Identity roles and test
accounts. Re-check before assuming fixtures exist; `SeedData` in appsettings is
`false` and the seeder is Development-only and opt-in.

Update 2026-09-13: the LocalDB instance is stopped and refuses this Windows login,
but `.\SQLEXPRESS` holds `GPIA_Auth`/`GPIA_Project` plus `GPIA_Auth_QA`/`GPIA_Project_QA`.
The running APIs (`dotnet *.Api.dll`) served 14 projects, which matches `GPIA_Project_QA`
(14 projects, 68 units, 42 memberships). QA has `e2e.{admin,agent,buyer,notary,tech}@qa.local`
accounts; their passwords are not recorded in either repo. Plain `GPIA_Project` on SQLEXPRESS
is an older schema (no `ProjectMemberships` table).

Two tooling traps on this machine (still current):
- `sqlcmd` needs `-I` (filtered index on `AspNetRoles`) **and** `-f 65001` when the
  `.sql` file contains accents, or it reads UTF-8 as ANSI and stores mojibake.
- Windows PowerShell 5.1 parses `.ps1` as ANSI unless the file has a UTF-8 **BOM** —
  non-ASCII literals in a script cause parse errors, not mangled output.

Related: [[rbac-spec6-implementation]], [[gpia-spec-document]], [[projectapi-sdk-pin]]
