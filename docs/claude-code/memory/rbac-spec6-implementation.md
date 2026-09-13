---
name: rbac-spec6-implementation
description: Ongoing §6 RBAC work spans two repos; progress is tracked in RBAC_PROGRESS.md in the backend repo
metadata: 
  node_type: memory
  type: project
  originSessionId: ea59dcf3-33cf-4b1c-8c52-f7b32070feec
  modified: 2026-07-24T18:08:20.783Z
---

The §6 "Acteurs et gestion des accès" implementation spans two repos:

- Backend: `C:\Users\LEGION\source\repos\whiredead\realestate` (AuthenticationAPI + ProjectAPI)
- Frontend: `C:\Users\LEGION\Desktop\realestateFront` (this repo)

Running progress log lives at `C:\Users\LEGION\source\repos\whiredead\realestate\RBAC_PROGRESS.md`
— read it at the start of any session touching roles/permissions. As of 2026-07-24 it covers steps
1–5 done, 6–10 remaining, and records the exact stopping point.

Key design decision already made and live: the JWT carries **both** the legacy role label
(`Admin`, `Agent`, `Notaire`, `Acheteur`) and the §6.1 spec code (`GLOBAL_ADMIN`, `SALES_AGENT`,
`NOTARY`, `BUYER`), while the login *response* returns spec codes only.

**Why:** emitting only spec codes would break every existing `[Authorize(Roles="Admin")]`;
emitting only legacy labels would make the 8-role matrix inapplicable.

**How to apply:** frontend must speak spec codes (the login response vocabulary), not legacy
labels. New backend authorization rules should be written against spec codes too — legacy labels
in the JWT exist only to keep untouched controllers working during migration.

Related: [[gpia-spec-document]]
