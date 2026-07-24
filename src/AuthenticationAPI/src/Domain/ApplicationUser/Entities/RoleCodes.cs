namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

/// <summary>
/// Role codes defined by spec §6.1.
///
/// The eight codes below are the specification's vocabulary. The database and
/// the existing JWTs still carry the legacy French labels this project shipped
/// with (`Admin`, `Agent`, `Notaire`, `Acheteur`), so <see cref="Normalize"/>
/// maps a legacy label onto its spec code. Authorisation MUST be expressed in
/// spec codes; legacy strings are an input format, never a decision input.
///
/// Two mappings are deliberately lossy and documented as such:
///   - legacy `Admin` maps to <see cref="GlobalAdmin"/>: the legacy model has a
///     single administrator level, and §6.4 forbids a project admin from
///     granting themselves global rights — so silently treating an existing
///     `Admin` as the *weaker* PROJECT_ADMIN would remove access people already
///     have, while treating them as GLOBAL_ADMIN preserves today's behaviour.
///     Splitting the two properly requires assigning real project scopes and is
///     tracked separately (§6.4 périmètre).
///   - legacy `Acheteur` maps to <see cref="Buyer"/>. The spec's PROSPECT role
///     (authenticated, no approved reservation yet) has no legacy equivalent;
///     it is derived at runtime, not stored (see <see cref="Prospect"/>).
/// </summary>
public static class RoleCodes
{
    /// <summary>Unauthenticated public user (§6.1). Never stored on an account.</summary>
    public const string Visitor = "VISITOR";

    /// <summary>Authenticated user with no approved reservation yet (§6.1).</summary>
    public const string Prospect = "PROSPECT";

    /// <summary>Holder of at least one approved reservation (§6.1, §6.2).</summary>
    public const string Buyer = "BUYER";

    /// <summary>Commercial agent: prospects, visits, reservations, pre-delivery (§6.1).</summary>
    public const string SalesAgent = "SALES_AGENT";

    /// <summary>After-sales technician, sole owner of warranty claims (§6.1, §6.4).</summary>
    public const string Technician = "TECHNICIAN";

    /// <summary>Notary: own availability and their projects' notary appointments (§6.1).</summary>
    public const string Notary = "NOTARY";

    /// <summary>Administrator of one or more assigned projects (§6.1).</summary>
    public const string ProjectAdmin = "PROJECT_ADMIN";

    /// <summary>Platform-wide administrator (§6.1).</summary>
    public const string GlobalAdmin = "GLOBAL_ADMIN";

    /// <summary>Every role that may be stored on an account (VISITOR is not).</summary>
    public static readonly string[] Assignable =
    {
        Prospect, Buyer, SalesAgent, Technician, Notary, ProjectAdmin, GlobalAdmin
    };

    /// <summary>Roles that grant access to the internal CRM at all (§6.3).</summary>
    public static readonly string[] Internal =
    {
        SalesAgent, Technician, Notary, ProjectAdmin, GlobalAdmin
    };

    /// <summary>Either administrator level (§6.3 "Admin projet" / "Admin global").</summary>
    public static readonly string[] AnyAdmin = { ProjectAdmin, GlobalAdmin };

    private static readonly IReadOnlyDictionary<string, string> LegacyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = GlobalAdmin,
            ["Agent"] = SalesAgent,
            ["Notaire"] = Notary,
            ["Acheteur"] = Buyer,
            ["Technicien"] = Technician,
            // Legacy codes that were never used by any account and carry no
            // spec meaning. Mapped to PROSPECT (the least-privileged stored
            // role) rather than dropped, so an unexpected value can never
            // silently widen access.
            ["AgentBch"] = Prospect,
            ["Observer"] = Prospect,
            ["SecurityOfficer"] = Prospect,
            ["Other"] = Prospect
        };

    /// <summary>
    /// Maps a stored/legacy role label onto its spec §6.1 code. Unknown values
    /// return <see cref="Prospect"/>: failing closed, never open.
    /// </summary>
    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Prospect;
        if (Assignable.Contains(role, StringComparer.OrdinalIgnoreCase)
            || string.Equals(role, Visitor, StringComparison.OrdinalIgnoreCase))
        {
            // Already a spec code — return the canonical casing.
            return Assignable.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase))
                   ?? Visitor;
        }
        return LegacyMap.TryGetValue(role, out var mapped) ? mapped : Prospect;
    }

    /// <summary>Normalises a whole role list, de-duplicated.</summary>
    public static string[] Normalize(IEnumerable<string>? roles) =>
        (roles ?? Enumerable.Empty<string>())
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
