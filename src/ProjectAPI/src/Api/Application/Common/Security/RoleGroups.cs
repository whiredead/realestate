using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Common.Security;

/// <summary>
/// Comma-joined role-code groups for <c>[Authorize(Roles = …)]</c>, derived from
/// the §6.3 authorization matrix. These are compile-time constants (all
/// <see cref="RoleCodes"/> members are <c>const</c>), so they are usable in
/// attributes.
///
/// Role attributes are the coarse gate ("which kinds of user may call this at
/// all"). They do NOT enforce the project perimeter — an agent assigned to
/// project A still passes a <see cref="AdminsAgents"/> check on project B. The
/// perimeter is <see cref="ProjectScopeService"/>'s job, called inside the
/// handlers. Both layers are required by §6.4.
///
/// The JWT carries both the legacy label and the spec code (see TokenProvider),
/// so an account stored as legacy "Agent" still matches <see cref="RoleCodes.SalesAgent"/>.
/// </summary>
public static class RoleGroups
{
    /// <summary>§6.3 "Admin projet" / "Admin global".</summary>
    public const string Admins = RoleCodes.GlobalAdmin + "," + RoleCodes.ProjectAdmin;

    /// <summary>Only the platform-wide administrator (§6.3 "Paramétrage global").</summary>
    public const string GlobalOnly = RoleCodes.GlobalAdmin;

    /// <summary>Admins plus the commercial agent (CRM, reservations, visits).</summary>
    public const string AdminsAgents = Admins + "," + RoleCodes.SalesAgent;

    /// <summary>Admins, agent and notary (delivery, construction reads, notary appts).</summary>
    public const string AdminsAgentsNotary = AdminsAgents + "," + RoleCodes.Notary;

    /// <summary>Admins plus the after-sales technician (§6.3 SAV).</summary>
    public const string AdminsTechnicians = Admins + "," + RoleCodes.Technician;

    /// <summary>Admins plus notary (notary availability/appointment management).</summary>
    public const string AdminsNotary = Admins + "," + RoleCodes.Notary;

    /// <summary>
    /// §5.7 FR-NOT-003 — who may REQUEST a notarial appointment: the buyer, the
    /// responsible sales agent, or the project admin. Deliberately excludes the
    /// notary: they respond to a request, they do not raise one for themselves.
    /// </summary>
    public const string NotaryAppointmentRequesters = AdminsAgents + "," + RoleCodes.Buyer;
}
