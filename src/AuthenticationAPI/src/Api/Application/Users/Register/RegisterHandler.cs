using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationAPI.Api.Application.Users.Register;

/// <summary>
/// Handles the registration command and processes user registration.
///
/// PROSPECT only (Phase 0) — see the guard at the top of Handle for why no
/// internal role, no BUYER, and no caller identity, can widen that. BUYER is
/// never a public-registration outcome: it is granted exactly two ways —
/// automatically after an administrator approves a reservation (existing
/// buyer-invitation flow), or via the Phase 2 invitation-acceptance flow for
/// a buyer who was invited directly. Letting the public endpoint mint BUYER
/// on request would let anyone claim buyer status without ever holding an
/// approved reservation.
/// </summary>
public class RegisterHandler : IRequestHandler<RegisterCommand, string>
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;

    public RegisterHandler(UserManager<User> userManager, IEmailService emailService)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    /// <summary>
    /// Handles the registration command and processes user registration.
    /// </summary>
    /// <param name="request">The RegisterCommand request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A string indicating the result of the registration operation.</returns>
    public async Task<string> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // §6.2/Phase 0 — public self-registration creates ONLY a PROSPECT, full
        // stop. Internal accounts (agent, technician, notary, project/global
        // admin) are never created here, not even by an authenticated admin:
        // that used to be gated on "is the caller *some* admin", which let a
        // PROJECT_ADMIN mint a GLOBAL_ADMIN or another PROJECT_ADMIN (the
        // caller-is-admin check never looked at *which* role was being
        // granted — the endpoint was reachable by anonymous callers too, so
        // the bug was not "who can call this" but "what this handler let a
        // caller create"). Internal accounts are created via
        // POST /api/User/admin-create (see Users/CreateUserByAdmin), which
        // also provisions the mirrored row in ProjectAPI so the same Id
        // resolves in both services' FKs — every internal-account creation
        // path MUST go through that same provisioning call, or ProjectAPI's
        // ProjectScopeService silently blocks the new account the moment it
        // tries to act (confirmed: a technician created outside this path
        // logs in fine but gets 403 PROJECT_SCOPE_DENIED on every claim
        // action). There is no separate internal-invitation flow today —
        // a bare `InternalInvitation` entity exists in ProjectAPI's domain
        // model but has no handler/controller built on top of it yet.
        // The endpoint stays [AllowAnonymous]: it must behave identically for
        // every caller, signed in or not, so nobody can rely on a bearer
        // token to unlock a path this handler no longer has.
        //
        // BUYER is likewise never a request-driven outcome of this endpoint:
        // it is granted automatically after an administrator approves a
        // reservation, or via the Phase 2 invitation-acceptance flow — never
        // by a caller simply asking for it. Whatever request.Roles contains,
        // the account created here is always PROSPECT; a request for BUYER
        // or any internal role is rejected rather than silently downgraded,
        // so a caller expecting BUYER learns immediately that this is not how
        // buyer status is granted.
        var requestedRoles = RoleCodes.Normalize(request.Roles);
        var wantsInternalRole = requestedRoles.Any(
            r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        var wantsBuyer = requestedRoles.Contains(RoleCodes.Buyer, StringComparer.Ordinal);
        if (wantsInternalRole)
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure("Roles",
                    "Les comptes internes sont créés uniquement par invitation (voir /api/User/invitations).")
            });
        }
        if (wantsBuyer)
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure("Roles",
                    "Le statut Acheteur est attribué automatiquement après approbation d'une réservation, ou par invitation, jamais à l'inscription.")
            });
        }

        // Map request to your User entity
        var user = request.Adapt<User>();

        // The Arabic name columns are NOT NULL in the schema, but the fields are
        // optional on the command (the §6.2 sign-up form never collects them —
        // the spec's bilingual scope is FR/EN). Coalesce rather than migrate the
        // columns: callers stay free to omit them, the insert stays valid.
        user.FirstNameAr ??= string.Empty;
        user.LastNameAr ??= string.Empty;

        // RegisterCommand.Id is a non-nullable Guid, so a caller that omits it
        // sends Guid.Empty — which Mapster copies straight onto User.Id (a
        // string key). Every such registration then collides on the primary key
        // and only the very first account on a fresh database can be created;
        // the rest surface as an opaque 500. Assign a real id when none was
        // supplied, and keep honouring an explicit one for seed scripts.
        if (string.IsNullOrWhiteSpace(user.Id) || user.Id == Guid.Empty.ToString())
        {
            user.Id = Guid.NewGuid().ToString();
        }

        // §6.1/Phase 0 — a request that reaches this line has already been
        // proven to contain neither an internal role nor BUYER, so the only
        // outcome this endpoint can ever produce is PROSPECT. Hard-code it
        // rather than persisting requestedRoles: that keeps the guard above
        // and the write below from ever drifting apart again, the way
        // Discriminator/AddToRolesAsync once wrote the caller's raw,
        // possibly-legacy strings ("Notaire" instead of "NOTARY") while the
        // authorization decision used the normalized form.
        user.Discriminator = RoleCodes.Prospect;

        // Create the user
        var creation = await _userManager.CreateAsync(user, request.Password);
        if (!creation.Succeeded)
        {
            // Throw detailed validation exception
            var failures = creation.Errors
                .Select(e => new ValidationFailure(e.Code, e.Description));
            throw new Common.Exceptions.ValidationException(failures);
        }

        // Same reasoning as Discriminator above: PROSPECT, always, never
        // whatever the caller asked for.
        await _userManager.AddToRolesAsync(user, new[] { RoleCodes.Prospect });

        // The Agent-PerformanceIndicator and Notary-WeeklyAvailability seeding
        // that used to live here is gone along with SALES_AGENT/NOTARY
        // self-registration above: this handler only ever produces PROSPECT
        // now. That per-role bootstrap, and BUYER's own bootstrap, belong to
        // the Phase 2 invitation-acceptance handler and the existing
        // reservation-approval flow respectively — never to this one.
        return user.Id;
    }
}
