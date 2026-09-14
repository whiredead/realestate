# Controller / action manifest

> **Machine-generated** by a script that strips `/* */` and `//` comments
> before matching `[Http*]` attributes, so commented-out actions are never
> counted. Regenerate rather than hand-edit.

## Reconciliation

| Measure | Count |
|---|---|
| `[Http*]` attribute occurrences in raw source | **170** |
| Occurrences inside comment blocks (not routed) | **2** |
| **Live routed actions** | **168** |

- ProjectAPI: **149** actions across **26** controllers
- AuthenticationAPI: **19** actions across **3** controllers

The only discrepancy is **AppointmentsController**: 11 attribute occurrences,
9 routed. Two `[HttpGet]` actions (`user/{userId}`, `project/{projectId}`)
sit inside a `/* … */` block at lines 202-230. Two `ApiControllerBase` files
(one per service) declare no actions and are excluded from the controller counts.

## Per-controller summary

| Service | File | Class | Actions | Doc |
|---|---|---|---:|---|
| AuthenticationAPI | `InternalController.cs` | `InternalController` | 2 | pending |
| AuthenticationAPI | `OtpVerificationController.cs` | `OtpVerificationController` | 2 | pending |
| AuthenticationAPI | `UserController.cs` | `UserController` | 15 | pending |
| ProjectAPI | `AdminDashboardController.cs` | `AdminDashboardController` | 1 | pending |
| ProjectAPI | `AgentAvailabilityController.cs` | `AgentAvailabilityController` | 9 | pending |
| ProjectAPI | `AppointmentsController.cs` | `AppointmentsController` | 9 | [Appointments.md](Appointments.md) |
| ProjectAPI | `ClaimsController.cs` | `AfterSaleClaimsController` | 5 | pending |
| ProjectAPI | `ConstructionController.cs` | `ConstructionController` | 7 | pending |
| ProjectAPI | `DeliveriesController.cs` | `DeliveriesController` | 2 | pending |
| ProjectAPI | `FeedbackController.cs` | `FeedbackController` | 3 | pending |
| ProjectAPI | `FileController.cs` | `FileController` | 4 | pending |
| ProjectAPI | `FinalVisitsController.cs` | `FinalVisitsController` | 8 | pending |
| ProjectAPI | `HandoversController.cs` | `HandoversController` | 6 | pending |
| ProjectAPI | `ImmeubleController.cs` | `ImmeubleController` | 17 | [Immeuble.md](Immeuble.md) |
| ProjectAPI | `ImportsController.cs` | `ImportsController` | 3 | pending |
| ProjectAPI | `InternalController.cs` | `InternalController` | 1 | pending |
| ProjectAPI | `InvitationsController.cs` | `InvitationsController` | 1 | pending |
| ProjectAPI | `LeadsController.cs` | `LeadsController` | 1 | pending |
| ProjectAPI | `NotaryAppointmentsController.cs` | `NotaryAppointmentsController` | 6 | [NotaryAppointments.md](NotaryAppointments.md) |
| ProjectAPI | `NotaryBlocksController.cs` | `NotaryBlocksController` | 5 | pending |
| ProjectAPI | `NotificationsController.cs` | `NotificationsController` | 2 | pending |
| ProjectAPI | `PaymentsController.cs` | `PaymentsController` | 7 | [Payments.md](Payments.md) |
| ProjectAPI | `ProjectAgentAssignmentConfigController.cs` | `ProjectAgentAssignmentConfigController` | 2 | pending |
| ProjectAPI | `ProjectAssignmentController.cs` | `ProjectAssignmentController` | 6 | pending |
| ProjectAPI | `ProjectController.cs` | `ProjectsController` | 23 | [Projects.md](Projects.md) |
| ProjectAPI | `ProjectMembershipController.cs` | `ProjectMembershipController` | 4 | pending |
| ProjectAPI | `ReservationsController.cs` | `ReservationsController` | 12 | [Reservations.md](Reservations.md) |
| ProjectAPI | `SalesController.cs` | `SalesController` | 2 | pending |
| ProjectAPI | `TypeBiensController.cs` | `TypeBiensController` | 3 | pending |
| | | **TOTAL** | **168** | **6 of 29 documented** |

## Full action listing

### ProjectAPI

#### `AdminDashboardController`  ·  `AdminDashboardController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/AdminDashboard/overview` | `GetDashboardOverview` |

#### `AgentAvailabilityController`  ·  `AgentAvailabilityController.cs`  ·  route prefix `api/agents/{agentId}`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/agents/{agentId}/blocks` | `CreateBlock` |
| DELETE | `/api/agents/{agentId}/blocks/{blockId:guid}` | `DeleteBlock` |
| GET | `/api/agents/{agentId}/blocks` | `GetBlocks` |
| PUT | `/api/agents/{agentId}/weekly-availability` | `SetWeeklyAvailability` |
| GET | `/api/agents/{agentId}/weekly-availability` | `GetWeeklyAvailability` |
| POST | `/api/agents/{agentId}/date-overrides` | `CreateDateOverride` |
| DELETE | `/api/agents/{agentId}/date-overrides/{overrideId:guid}` | `DeleteDateOverride` |
| PUT | `/api/agents/{agentId}/appointment-settings` | `SetAppointmentSettings` |
| GET | `/api/agents/{agentId}/available-slots` | `GetAvailableSlots` |

#### `AppointmentsController`  ·  `AppointmentsController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/Appointments` | `CreateAppointment` |
| GET | `/api/Appointments` | `GetAppointments` |
| GET | `/api/Appointments/mine` | `GetMyAppointments` |
| GET | `/api/Appointments/{appointmentId:guid}` | `GetAppointmentById` |
| PATCH | `/api/Appointments/{appointmentId}/status` | `UpdateAppointmentStatus` |
| GET | `/api/Appointments/{appointmentId}/assignment-history` | `GetAssignmentHistory` |
| POST | `/api/Appointments/{appointmentId}/visit-report` | `SubmitVisitReport` |
| GET | `/api/Appointments/{appointmentId}/visit-report` | `GetVisitReport` |
| GET | `/api/Appointments/{appointmentId}/visit-report/mine` | `GetVisitReportForBuyer` |

#### `AfterSaleClaimsController`  ·  `ClaimsController.cs`  ·  route prefix `api/after-sales/claims`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/after-sales/claims` | `Create` |
| GET | `/api/after-sales/claims` | `Get` |
| GET | `/api/after-sales/claims/mine` | `GetMine` |
| PUT | `/api/after-sales/claims/{claimId:guid}/status` | `UpdateStatus` |
| POST | `/api/after-sales/claims/{claimId:guid}/respond` | `RespondToResolution` |

#### `ConstructionController`  ·  `ConstructionController.cs`  ·  route prefix `api/construction`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/construction/projects/{projectId:guid}` | `GetProjectConstruction` |
| GET | `/api/construction/units/{unitId:guid}/title` | `GetTitle` |
| PATCH | `/api/construction/units/{unitId:guid}/title` | `UpdateTitle` |
| POST | `/api/construction/projects/{projectId:guid}/milestones` | `AddMilestone` |
| PATCH | `/api/construction/milestones/{milestoneId:guid}/status` | `UpdateMilestoneStatus` |
| POST | `/api/construction/projects/{projectId:guid}/updates` | `PublishUpdate` |
| POST | `/api/construction/projects/{projectId:guid}/complete` | `CompleteProject` |

#### `DeliveriesController`  ·  `DeliveriesController.cs`  ·  route prefix `api/deliveries`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/deliveries` | `Schedule` |
| PATCH | `/api/deliveries/{deliveryId:guid}/status` | `UpdateStatus` |

#### `FeedbackController`  ·  `FeedbackController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/Feedback/submit-feedback` | `SubmitFeedback` |
| GET | `/api/Feedback/{feedbackId}` | `GetFeedbackById` |
| GET | `/api/Feedback/feedbacks` | `GetFeedbacks` |

#### `FileController`  ·  `FileController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/File/upload` | `UploadFiles` |
| DELETE | `/api/File/delete/{fileName}` | `DeleteFile` |
| PUT | `/api/File/update/{fileName}` | `UpdateFile` |
| GET | `/api/File/download/{fileName}` | `DownloadFile` |

#### `FinalVisitsController`  ·  `FinalVisitsController.cs`  ·  route prefix `api/final-visits`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/final-visits/reservations/{reservationId:guid}/request` | `Request` |
| POST | `/api/final-visits/appointments/{appointmentId:guid}/transition` | `TransitionAppointment` |
| POST | `/api/final-visits/appointments/{appointmentId:guid}/report` | `SubmitReport` |
| POST | `/api/final-visits/reports/{reportId:guid}/acknowledge` | `Acknowledge` |
| POST | `/api/final-visits/snags/{snagId:guid}/transition` | `TransitionSnag` |
| GET | `/api/final-visits/reservations/{reservationId:guid}/case` | `GetFinalVisitCase` |
| GET | `/api/final-visits/reservations/{reservationId:guid}/report/mine` | `GetMyFinalVisitReport` |
| GET | `/api/final-visits/reservations/{reservationId:guid}/notary-eligibility` | `NotaryEligibility` |

#### `HandoversController`  ·  `HandoversController.cs`  ·  route prefix `api/Handovers`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/Handovers` | `Schedule` |
| POST | `/api/Handovers/{id:guid}/confirm` | `Confirm` |
| POST | `/api/Handovers/{id:guid}/report` | `SubmitReport` |
| POST | `/api/Handovers/reports/{reportId:guid}/acknowledge` | `Acknowledge` |
| GET | `/api/Handovers/reservations/{reservationId:guid}/mine` | `GetMyHandoverStatus` |
| GET | `/api/Handovers/reservations/{reservationId:guid}/warranties/mine` | `GetMyWarranties` |

#### `ImmeubleController`  ·  `ImmeubleController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/Immeuble` | `CreateImmeuble` |
| GET | `/api/Immeuble` | `GetAllImmeubles` |
| GET | `/api/Immeuble/{id}` | `GetImmeubleById` |
| PUT | `/api/Immeuble/{id}` | `UpdateImmeuble` |
| DELETE | `/api/Immeuble/{id}` | `DeleteImmeuble` |
| POST | `/api/Immeuble/{immeubleId}/units` | `AddUnitsToImmeuble` |
| POST | `/api/Immeuble/{immeubleId}/floors` | `CreateFloor` |
| GET | `/api/Immeuble/{immeubleId}/floors` | `GetFloors` |
| GET | `/api/Immeuble/{immeubleId}/floor-stats` | `GetFloorStats` |
| GET | `/api/Immeuble/floors/{floorId}/units` | `GetUnitsByFloor` |
| GET | `/api/Immeuble/by-immeuble` | `GetUnitsByImmeubleId` |
| GET | `/api/Immeuble/all` | `GetAllUnits` |
| PUT | `/api/Immeuble/update/{id}` | `UpdateUnit` |
| POST | `/api/Immeuble/features` | `AddImmeubleFeatures` |
| GET | `/api/Immeuble/features` | `GetImmeubleFeatures` |
| POST | `/api/Immeuble/{immeubleId}/tracking` | `AddImmeubleTracking` |
| GET | `/api/Immeuble/{immeubleId}/tracking` | `GetImmeubleTracking` |

#### `ImportsController`  ·  `ImportsController.cs`  ·  route prefix `api/imports`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/imports/template` | `GetTemplate` |
| POST | `/api/imports/projects/{projectId:guid}/validate` | `Validate` |
| POST | `/api/imports/{batchId:guid}/commit` | `Commit` |

#### `InternalController`  ·  `InternalController.cs`  ·  route prefix `internal/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/internal/Internal/users/provision` | `ProvisionUser` |

#### `InvitationsController`  ·  `InvitationsController.cs`  ·  route prefix `api/invitations`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/invitations/accept` | `Accept` |

#### `LeadsController`  ·  `LeadsController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/Leads` | `GetLeads` |

#### `NotaryAppointmentsController`  ·  `NotaryAppointmentsController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/NotaryAppointments` | `CreateNotaryAppointment` |
| GET | `/api/NotaryAppointments/{id}` | `GetNotaryAppointmentById` |
| GET | `/api/NotaryAppointments` | `GetNotaryAppointments` |
| GET | `/api/NotaryAppointments/NotaireAvailability` | `GetNotaireAvailability` |
| PUT | `/api/NotaryAppointments/{id}` | `UpdateNotaryAppointment` |
| GET | `/api/NotaryAppointments/{id}/assignment-history` | `GetAssignmentHistory` |

#### `NotaryBlocksController`  ·  `NotaryBlocksController.cs`  ·  route prefix `api/notaries/{notaryId}`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/notaries/{notaryId}/blocks` | `Create` |
| DELETE | `/api/notaries/{notaryId}/blocks/{blockId:guid}` | `Delete` |
| GET | `/api/notaries/{notaryId}/blocks` | `GetRange` |
| PUT | `/api/notaries/{notaryId}/weekly-availability` | `SetWeeklyAvailability` |
| GET | `/api/notaries/{notaryId}/weekly-availability` | `GetWeeklyAvailability` |

#### `NotificationsController`  ·  `NotificationsController.cs`  ·  route prefix `api/notifications`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/notifications/mine` | `GetMine` |
| POST | `/api/notifications/{id:guid}/read` | `MarkRead` |

#### `PaymentsController`  ·  `PaymentsController.cs`  ·  route prefix `api/payments`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/payments/method-codes` | `GetMethodCodes` |
| GET | `/api/payments/reservations/{reservationId:guid}/schedule` | `GetSchedule` |
| POST | `/api/payments/reservations/{reservationId:guid}/schedule` | `CreateSchedule` |
| POST | `/api/payments/reservations/{reservationId:guid}/payments` | `RecordPayment` |
| POST | `/api/payments/{paymentId:guid}/reverse` | `ReversePayment` |
| POST | `/api/payments/{paymentId:guid}/validate` | `ValidatePayment` |
| POST | `/api/payments/{paymentId:guid}/reject` | `RejectPayment` |

#### `ProjectAgentAssignmentConfigController`  ·  `ProjectAgentAssignmentConfigController.cs`  ·  route prefix `api/projects/{projectId:guid}/agent-assignment-config`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/projects/{projectId:guid}/agent-assignment-config` | `Get` |
| PUT | `/api/projects/{projectId:guid}/agent-assignment-config` | `Set` |

#### `ProjectAssignmentController`  ·  `ProjectAssignmentController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/ProjectAssignment` | `Create` |
| DELETE | `/api/ProjectAssignment/UnassignAgentsNotaire` | `UnassignAgentsNotaire` |
| PUT | `/api/ProjectAssignment` | `Update` |
| DELETE | `/api/ProjectAssignment/{id}` | `Delete` |
| GET | `/api/ProjectAssignment/{id}` | `GetById` |
| GET | `/api/ProjectAssignment` | `GetAll` |

#### `ProjectsController`  ·  `ProjectController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/Projects` | `CreateProject` |
| GET | `/api/Projects` | `GetAllProjects` |
| GET | `/api/Projects/{id}` | `GetProjectById` |
| PUT | `/api/Projects/{id}` | `UpdateProject` |
| POST | `/api/Projects/Like` | `AddLikedProject` |
| DELETE | `/api/Projects/DisLikeProject` | `DisLikeProject` |
| GET | `/api/Projects/LikedProjects` | `GetLikedProjects` |
| PUT | `/api/Projects/LikedProject` | `UpdateLikedProject` |
| POST | `/api/Projects/features` | `AddProjectFeatures` |
| DELETE | `/api/Projects/RemoveFeatures` | `RemoveFeatures` |
| GET | `/api/Projects/quartier-amenities` | `GetQuartierAmenities` |
| GET | `/api/Projects/features` | `GetProjectFeatures` |
| POST | `/api/Projects/quartiers` | `CreateQuartier` |
| GET | `/api/Projects/quartiers` | `GetQuartiers` |
| GET | `/api/Projects/quartiers/{id}` | `GetQuartierById` |
| POST | `/api/Projects/{projectId}/videos` | `CreateEspaceTempsReel` |
| GET | `/api/Projects/videos/{id}` | `GetEspaceTempsReelById` |
| GET | `/api/Projects/{projectId}/videos` | `GetVideosByProjectId` |
| GET | `/api/Projects/user/{userId}` | `GetUserPurchases` |
| DELETE | `/api/Projects/{projectId}` | `RemoveProject` |
| POST | `/api/Projects/{projectId}/type-biens` | `AssociateTypeBienToProject` |
| GET | `/api/Projects/{projectId}/type-biens` | `GetTypeBiensByProject` |
| GET | `/api/Projects/type-biens` | `GetAllTypeBiens` |

#### `ProjectMembershipController`  ·  `ProjectMembershipController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/ProjectMembership` | `Create` |
| POST | `/api/ProjectMembership/{id}/end` | `End` |
| GET | `/api/ProjectMembership/{id}` | `GetById` |
| GET | `/api/ProjectMembership` | `GetAll` |

#### `ReservationsController`  ·  `ReservationsController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/Reservations/create` | `CreateReservation` |
| GET | `/api/Reservations/{id}` | `GetReservationById` |
| GET | `/api/Reservations/mine` | `GetMyReservations` |
| GET | `/api/Reservations/list` | `GetReservations` |
| POST | `/api/Reservations/{id:guid}/documents` | `UploadDocument` |
| DELETE | `/api/Reservations/documents/{documentId:guid}` | `DeleteDocument` |
| POST | `/api/Reservations/{id:guid}/approve` | `Approve` |
| POST | `/api/Reservations/{id:guid}/reject` | `Reject` |
| POST | `/api/Reservations/{id:guid}/request-changes` | `RequestChanges` |
| POST | `/api/Reservations/{id:guid}/submit` | `Submit` |
| PUT | `/api/Reservations/{id:guid}/assign-notaire` | `AssignNotaire` |
| PUT | `/api/Reservations/{id:guid}/cancel` | `CancelReservation` |

#### `SalesController`  ·  `SalesController.cs`  ·  route prefix `api/sales`

| Verb | Path | Action method |
|---|---|---|
| GET | `/api/sales` | `GetAll` |
| GET | `/api/sales/user/{userId}` | `GetByUser` |

#### `TypeBiensController`  ·  `TypeBiensController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/TypeBiens` | `CreateTypeBien` |
| PUT | `/api/TypeBiens/{id}` | `UpdateTypeBien` |
| DELETE | `/api/TypeBiens/{id}` | `DeleteTypeBien` |

### AuthenticationAPI

#### `InternalController`  ·  `InternalController.cs`  ·  route prefix `internal/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/internal/Internal/users/provision` | `ProvisionUser` |
| GET | `/internal/Internal/users/{userId}/roles` | `GetUserRoles` |

#### `OtpVerificationController`  ·  `OtpVerificationController.cs`  ·  route prefix `anonym-users`

| Verb | Path | Action method |
|---|---|---|
| POST | `/anonym-users/request-code` | `RequestVerificationCode` |
| POST | `/anonym-users/login` | `VerifyCode` |

#### `UserController`  ·  `UserController.cs`  ·  route prefix `api/[controller]`

| Verb | Path | Action method |
|---|---|---|
| POST | `/api/User/login` | `Login` |
| POST | `/api/User` | `Register` |
| POST | `/api/User/admin-create` | `CreateUserByAdmin` |
| POST | `/api/User/confirm-email` | `ConfirmEmail` |
| GET | `/api/User` | `GetUsers` |
| GET | `/api/User/by-role/{role}` | `GetUsersByRole` |
| GET | `/api/User/multiple` | `GetUsersByIds` |
| GET | `/api/User/roles` | `GetAllRoles` |
| GET | `/api/User/lockout/{id}` | `LockoutUser` |
| GET | `/api/User/unlock/{id}` | `UnlockUser` |
| POST | `/api/User/forgot-password` | `ForgotPassword` |
| POST | `/api/User/reset-password` | `ResetPassword` |
| POST | `/api/User/admin-change-password` | `AdminChangePassword` |
| DELETE | `/api/User/{id}` | `DeleteUser` |
| PUT | `/api/User/{id}` | `UpdateUser` |
