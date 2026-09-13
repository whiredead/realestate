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
    /// <remarks>
    /// Includes the technical lead. What each role may do to a claim is decided
    /// in the handler: a technician only acts on claims assigned to them, and
    /// only the <see cref="ClaimSupervisors"/> assign or validate closure.
    /// </remarks>
    public const string AdminsTechnicians = Admins + "," + RoleCodes.TechLead + "," + RoleCodes.Technician;

    /// <summary>Every internal role (no buyer / prospect) — project-scoped reads shared by all staff screens.</summary>
    public const string InternalStaff = AdminsAgentsNotary + "," + RoleCodes.TechLead + "," + RoleCodes.Technician;

    /// <summary>Who qualifies, assigns and validates the closure of a claim: admins and the technical lead.</summary>
    public const string ClaimSupervisors = Admins + "," + RoleCodes.TechLead;

    /// <summary>Admins plus notary (notary availability/appointment management).</summary>
    public const string AdminsNotary = Admins + "," + RoleCodes.Notary;

    /// <summary>
    /// §5.7 FR-NOT-003 — who may REQUEST a notarial appointment: the buyer, the
    /// responsible sales agent, or the project admin. Deliberately excludes the
    /// notary: they respond to a request, they do not raise one for themselves.
    /// </summary>
    public const string NotaryAppointmentRequesters = AdminsAgents + "," + RoleCodes.Buyer;

    /// <summary>
    /// §6.3 — who may READ a notarial appointment: the parties to it (buyer,
    /// responsible agent, notary) plus the admins.
    ///
    /// TECHNICIAN is deliberately absent. It is an internal role, so the
    /// handlers' §6.4 scope check treats it as staff and lets a technician with
    /// any project membership read that project's notary appointments — which
    /// carry the buyer's CIN, e-mail, phone number, the property price and the
    /// notarial fees. After-sales work never needs the deed file.
    /// </summary>
    public const string NotaryAppointmentReaders = AdminsAgentsNotary + "," + RoleCodes.Buyer;

    /// <summary>
    /// §6.3 — who may READ a sale file: the commercial side plus the buyer whose
    /// purchase it is.
    ///
    /// TECHNICIAN is excluded for the same reason as
    /// <see cref="NotaryAppointmentReaders"/>. The sale carries the buyer's
    /// name, e-mail, phone, CIN and the agreed price; the handlers class a
    /// technician as internal staff and let one read any sale on a project they
    /// hold a membership on (and, on GET /api/sales/user/{userId}, any buyer's
    /// whole purchase history regardless of project). After-sales work is
    /// bounded by §6.1 to warranty claims.
    /// </summary>
    public const string SaleReaders = AdminsAgents + "," + RoleCodes.Buyer;
}
