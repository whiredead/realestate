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
