---
name: gpia-spec-document
description: Location of the GPIA functional/technical spec .docx and how to convert it to readable text
metadata: 
  node_type: memory
  type: reference
  originSessionId: ea59dcf3-33cf-4b1c-8c52-f7b32070feec
  modified: 2026-07-25T20:53:14.456Z
---

The authoritative spec for this project is `GPIA_Specifications_Fonctionnelles_Techniques_Codex_v2.0`,
a Word file at `C:\Users\LEGION\Downloads\GPIA_Specifications_Fonctionnelles_Techniques_Codex_v2.0.docx`
(53 sections, ~166k chars of text, French).

It cannot be opened with the Read tool. To read it: copy to `.zip`, extract, and convert
`word/document.xml` to markdown (paragraphs via `w:pStyle` → headings/bullets, `w:tbl` → markdown
tables). Section numbering in the converted output matches the spec's own numbering, so
`## 6.3` etc. are greppable.

Sections that come up most: §6 roles + authorization matrix, §28 per-role navigation,
§29 screen inventory, §31 API contract, §37 end-to-end acceptance criteria (AC-001…AC-045),
§47 state-transition matrices, §48 logical data model (tables/columns/constraints),
§49 backend command contracts, §51 traceability matrix.

A distilled English summary (workflows, roles, state machines, table relations) lives in the
repo at `GPIA_WORKFLOW.md` — written 2026-07-25 to be fed to Claude Code as context.

Related: [[rbac-spec6-implementation]]
