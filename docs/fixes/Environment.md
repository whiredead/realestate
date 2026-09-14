# Environment changes

## Development database moved to Azure SQL
- **Files:** `src/AuthenticationAPI/src/Api/appsettings.json`, `src/ProjectAPI/src/Api/appsettings.json`
- **Wrong:** both APIs pointed at LocalDB; the Azure databases `GPIA_Project` / `GPIA_Auth` on `sql-gpia1539` were 6 migrations behind (`AddSaleLifecycleAndProjectWarranty` … `AddClaimCommentKindAndAttachmentPhase`).
- **Changed:** copies `GPIA_Project_backup_20260914` and `GPIA_Auth_backup_20260914` taken first (rollback point); the 6 pending migrations and `AddQuartierFeatures` applied to `GPIA_Project` (data preserved: 4 projects, 321 units, 203 reservations, 162 sales — existing sales backfilled to Confirmed); `SqlPrimary` in both appsettings now targets Azure (MARS on). The E2E accounts (e2e.* / gpia.*) and the TECH_LEAD role were copied into both Azure databases (insert-only, existing users untouched).
- **Also:** `AUTO_CLOSE` turned off on the local QA databases (each idle close flushed the plan cache).

## Azure database scaled up
- **Changed:** `GPIA_Project` moved from Basic (5 DTU) to Standard **S2** (50 DTU): during a full E2E run the Basic tier sat at 100 % CPU for 25 minutes and screens timed out. `GPIA_Auth` stays Basic. Scale back with `ALTER DATABASE [GPIA_Project] MODIFY (EDITION='Basic', SERVICE_OBJECTIVE='Basic')` when not testing.
