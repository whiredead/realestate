---
name: projectapi-sdk-pin
description: ProjectAPI's global.json pins .NET SDK 8.0.423 which is not installed; build works only when cwd is the repo root
metadata: 
  node_type: memory
  type: project
  originSessionId: b05a3983-ffdb-44fa-80c7-60a7f4d9c31b
  modified: 2026-09-12T16:47:52.376Z
---

`src/ProjectAPI/global.json` pins `sdk.version` to **8.0.423**, but the only SDK
installed on this machine is **9.0.309**. Running `dotnet build` with the working
directory inside `src/ProjectAPI/` fails before compiling: "A compatible .NET SDK
was not found."

SDK resolution keys off the **current working directory**, not the project path,
and the repo root has no `global.json`. So the workaround is to build from the
repo root:

```
cd C:\Users\LEGION\source\repos\whiredead\realestate
dotnet build src/ProjectAPI/src/Api/ProjectAPI.Api.csproj
```

That succeeds: 0 errors (314 warnings, ~500 of them CS8618 nullable-property
noise on EF entities). AuthenticationAPI has no `global.json` at all and builds
anywhere. Both projects target `net8.0` and compile fine under SDK 9.

**Why:** the pin looks like a hard toolchain requirement but isn't — nothing in
either project needs SDK 8. Deleting the file, or adding
`"rollForward": "latestMajor"`, unblocks it permanently.

Related: [[local-dev-environment]]
