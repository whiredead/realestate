# Fixes — After-sales claims

## Technician could assign, reject and close claims
- **Files:** `src/ProjectAPI/src/Api/Application/Sales/AfterSales/UpdateClaimStatus/UpdateClaimStatusHandler.cs`
- **Wrong:** any TECHNICIAN assigned to a claim could move it to any state the state machine allowed, including `Closed` (validation of closure) and `Assigned`.
- **Changed:** supervisors (GLOBAL_ADMIN, PROJECT_ADMIN, TECH_LEAD) may apply any valid transition. A technician must be the assignee and may only target `InProgress`, `WaitingCustomer`, `Resolved`; anything else → 403 `UNAUTHORIZED`.

## Technical lead restricted like a technician when listing
- **Files:** `src/ProjectAPI/src/Api/Application/Sales/AfterSales/GetClaims/GetClaimsHandler.cs`
- **Wrong:** the "assigned to me only" filter applied to anyone holding TECHNICIAN, even when also a supervisor.
- **Changed:** the filter applies only to technicians who are not TECH_LEAD / admin; supervisors see their project perimeter.

## Claim list did not expose the assignee
- **Files:** `GetClaims/AfterSaleClaimResponse.cs`, `GetClaims/GetClaimsHandler.cs`
- **Wrong:** the SAV screen could not know whether the signed-in technician was the assignee, so it offered every action to everyone.
- **Changed:** `AssignedAgentId` added to the response.

## State machine did not match the spec workflow
- **Files:** `src/ProjectAPI/src/Domain/Sales/Entities/AfterSaleClaim.cs` (`ClaimStateMachine`), `UpdateClaimStatus/UpdateClaimStatusHandler.cs`
- **Wrong:** SUBMITTED had to go through UNDER_REVIEW before ASSIGNED (qualification by the lead before assignment); spec order is SOUMISE → ASSIGNÉE → EN COURS D'EXAMEN → EN RÉSOLUTION → RÉSOLUE → VALIDÉE/FERMÉE.
- **Changed:** Submitted → Assigned/Rejected/Cancelled; Assigned → UnderReview/Cancelled; UnderReview → InProgress/MoreInfoRequired/Rejected; MoreInfoRequired → UnderReview/Cancelled; InProgress → WaitingCustomer/Resolved; WaitingCustomer → InProgress; Resolved → InProgress/Closed. Technician targets: UnderReview, MoreInfoRequired, InProgress, WaitingCustomer, Resolved (never Assigned/Rejected/Closed).

## No claim detail, no comments, no photo phase; any internal role could read evidence
- **Files:** new `Application/Sales/AfterSales/ClaimDetail/ClaimDetailCommands.cs`; `Controllers/ClaimsController.cs`; `Attachments/UploadClaimAttachment*`, `Attachments/DownloadClaimAttachment*`; `Domain/Sales/Entities/ClaimComment.cs` (`Kind`), `ClaimAttachment.cs` (`Phase`); migration `20260913233823_AddClaimCommentKindAndAttachmentPhase`
- **Wrong:** no endpoint returned one claim with its history, comments and attachments; comments had no kind (work done vs. reply); attachments had no before/after phase; upload/download let ANY internal role (agent, notary, unassigned technician) read and add evidence on any claim.
- **Changed:** `GET after-sales/claims/{id}` (detail + unit context), `POST …/{id}/comments` (WORK_DONE/NOTE for staff, always REPLY for the buyer; 409 once closed/cancelled/rejected; 422 empty or > 2000 chars), upload accepts `Phase` BEFORE/AFTER. `ClaimAccessPolicy`: owner, assigned technician, or supervisor within the project perimeter — others get 403.

## Buyer could not know which units were open to a claim
- **Files:** `ClaimDetail/ClaimDetailCommands.cs` (`GetClaimEligibleUnitsQuery`), `Controllers/ClaimsController.cs`
- **Wrong:** the portal had to guess from reservations, offering units the API then refused.
- **Changed:** `GET after-sales/claims/eligible-units`: the caller's units with a confirmed sale, delivered, with an active warranty, each with its sale id, warranty end and project / building / floor / unit context.

## Claim creation: ownership checked on any past reservation
- **Files:** `CreateAfterSaleClaim/CreateAfterSaleClaimHandler.cs`
- **Wrong:** a buyer who once had a (cancelled) reservation on the unit passed the ownership check.
- **Changed:** ownership is the confirmed sale's buyer / reservation; a confirmed sale is required (409 `PROPERTY_NOT_DELIVERED` otherwise), then an active warranty (409 `WARRANTY_EXPIRED`); anyone else → 403.
