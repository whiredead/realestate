# Fixes — Final visit (ProjectAPI)

## Spec fields missing from the visit report
- **Files:** `src/ProjectAPI/src/Domain/FinalVisits/Entities/Snag.cs` (`FinalVisitReport`), migration `Infrastructure/Migrations/20260913222214_AddFinalVisitReportFeedbackFields`, `Application/FinalVisits/SubmitFinalVisitReport/SubmitFinalVisitReportCommand.cs`, `GetFinalVisitCase/*`, `GetMyFinalVisitReport/*`
- **Wrong:** the report had no client feedback, no reason ("motif"), no corrective action and no follow-up ("suivi"); the case DTO did not expose the responsible sales agent.
- **Changed:** new nullable columns `ClientFeedback`, `NonComplianceReason`, `CorrectiveAction`, `FollowUpNotes` on `FinalVisitReports` (migration applied to GPIA_Project_QA). Exposed on the internal case DTO (with `ResponsibleSalesAgentId/Name`, appointment `CauseType/CauseDescription`) and on the buyer's read-only `/report/mine`.

## Unsatisfactory visit accepted without a reason or corrective action
- **Files:** `SubmitFinalVisitReportCommand.cs`
- **Changed:** result `NonCompliantMajorSnags` / `NonCompliantBlockingSnags` requires `NonComplianceReason` and `CorrectiveAction` → 422 with a field error for each missing one.

## Client absent: no way out of NO_SHOW but a new request
- **Files:** `src/ProjectAPI/src/Domain/FinalVisits/Entities/FinalVisitCase.cs` (`AppointmentStateMachine`)
- **Wrong:** `NoShow` was terminal with no follow-up: the attempt could neither be closed nor explicitly rescheduled from the agent screen.
- **Changed:** `NoShow → Cancelled` allowed (reason still mandatory); rescheduling remains a new attempt (`RequestFinalVisit`, now driven from the agent screen with `CauseType = NO_SHOW`).
