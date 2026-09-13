# Fixes — Users & roles

## TECH_LEAD role ("responsable technique") added
- **Files:** `src/AuthenticationAPI/src/Domain/ApplicationUser/Entities/RoleCodes.cs`, `src/ProjectAPI/src/Domain/Users/Entities/RoleCodes.cs`, `src/AuthenticationAPI/src/Api/Application/Users/Register/RegisterValidator.cs`, `src/ProjectAPI/src/Api/Application/Common/Security/RoleGroups.cs`
- **Wrong:** the spec's technical lead (assigns claims, validates closure) had no role; only TECHNICIAN existed, and it could do both.
- **Changed:** new code `TECH_LEAD` in `Assignable`, `Internal` and `MembershipRoles` (project-scoped through memberships). `RoleGroups.AdminsTechnicians` now includes it; new `RoleGroups.ClaimSupervisors` = admins + TECH_LEAD.

## Admin account creation crashed for a role that did not exist yet
- **Files:** `src/AuthenticationAPI/src/Api/Application/Users/CreateUserByAdmin/CreateUserByAdminHandler.cs`, `src/AuthenticationAPI/src/Api/Application/Internal/ProvisionInternalUser/ProvisionInternalUserHandler.cs`
- **Wrong:** `new Role { Name, DisplayName }` has no `Id` (`IdentityRole<string>` does not generate one) → `InvalidOperationException: Unable to track an entity of type 'Role'`. In `CreateUserByAdmin` the user was saved *before* the role was created, so the failure left an account with no role and never provisioned in ProjectAPI.
- **Changed:** roles are created with `Id = Guid.NewGuid()`; in `CreateUserByAdmin` the role is ensured before the user is written.
