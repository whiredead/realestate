using Moq;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Identity.Users.Register;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Common.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;

namespace Men.ProfessorAssignmentApi.Api.Tests.Unit;

/// <summary>
/// Phase 0 regression tests for RegisterHandler: the public, [AllowAnonymous]
/// registration endpoint must never be able to mint an internal account
/// (SALES_AGENT, TECHNICIAN, NOTARY, PROJECT_ADMIN, GLOBAL_ADMIN) or a BUYER
/// account — BUYER is granted only by reservation approval or the Phase 2
/// invitation-acceptance flow, never by a public request — and must never
/// persist a raw/legacy role label instead of its canonical §6.1 code.
/// </summary>
public class RegisterHandlerTests
{
    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager
            .Setup(m => m.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        return userManager;
    }

    private static RegisterCommand BuildCommand(List<string> roles) => new()
    {
        UserName = "jdoe",
        Password = "P@ssw0rd!",
        FirstName = "Jane",
        LastName = "Doe",
        PhoneNumber = "0600000000",
        Email = "jane.doe@example.com",
        Roles = roles
    };

    [Theory]
    [InlineData("SALES_AGENT")]
    [InlineData("TECHNICIAN")]
    [InlineData("NOTARY")]
    [InlineData("PROJECT_ADMIN")]
    [InlineData("GLOBAL_ADMIN")]
    public async Task Handle_AnonymousRequestingInternalRole_ThrowsValidationException(string internalRole)
    {
        // The endpoint is [AllowAnonymous] and RegisterHandler receives no
        // caller identity at all — there is no "authenticated admin" bypass
        // to test against, because none exists any more. Any caller, signed
        // in or not, requesting an internal role must be rejected.
        var userManager = CreateUserManagerMock();
        var emailService = new Mock<IEmailService>();
        var handler = new RegisterHandler(userManager.Object, emailService.Object);
        var command = BuildCommand(new List<string> { internalRole });

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Agent", "SALES_AGENT")]
    [InlineData("Notaire", "NOTARY")]
    [InlineData("Admin", "GLOBAL_ADMIN")]
    [InlineData("Technicien", "TECHNICIAN")]
    public async Task Handle_LegacyInternalRoleLabel_NormalizesAndIsRejected(string legacyLabel, string expectedInternalCode)
    {
        // Legacy French labels must be normalized to their §6.1 code BEFORE the
        // internal-role guard runs — a caller cannot bypass the block simply
        // by sending the old vocabulary instead of the new one. This also
        // pins down that the mapping used for the *security decision* is the
        // same mapping that would otherwise be persisted (see next test).
        RoleCodes.Normalize(legacyLabel).Should().Be(expectedInternalCode);

        var userManager = CreateUserManagerMock();
        var emailService = new Mock<IEmailService>();
        var handler = new RegisterHandler(userManager.Object, emailService.Object);
        var command = BuildCommand(new List<string> { legacyLabel });

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Acheteur")]
    [InlineData("BUYER")]
    public async Task Handle_AnyCallerRequestingBuyer_ThrowsValidationException(string buyerLabel)
    {
        // BUYER is a consequence of an approved reservation (or the Phase 2
        // invitation-acceptance flow for a directly-invited buyer), never a
        // choice made at public sign-up. A caller asking for it — whether via
        // the canonical code or the legacy "Acheteur" label — must be
        // rejected exactly like an internal-role request, not silently
        // downgraded to PROSPECT.
        var userManager = CreateUserManagerMock();
        var emailService = new Mock<IEmailService>();
        var handler = new RegisterHandler(userManager.Object, emailService.Object);
        var command = BuildCommand(new List<string> { buyerLabel });

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("PROSPECT")]
    [InlineData("Observer")]
    [InlineData("AgentBch")]
    public async Task Handle_PublicRequest_AlwaysPersistsProspect_RegardlessOfRequestedLabel(string requestedLabel)
    {
        // Every request that clears the internal-role and BUYER guards must
        // produce exactly PROSPECT — never whatever label the caller sent,
        // even when that label normalizes to PROSPECT anyway (e.g. the
        // legacy no-meaning codes). The role passed to AddToRolesAsync is
        // hard-coded, not derived from the request, and the entity is built as
        // a plain User — whose CLR type IS the stored TPH discriminator — so
        // this also proves the two writes can never drift apart again.
        User? persistedUser = null;
        IEnumerable<string>? persistedRoles = null;

        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .Callback<User, string>((u, _) => persistedUser = u)
            .ReturnsAsync(IdentityResult.Success);
        userManager
            .Setup(m => m.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()))
            .Callback<User, IEnumerable<string>>((_, roles) => persistedRoles = roles)
            .ReturnsAsync(IdentityResult.Success);

        var emailService = new Mock<IEmailService>();
        var handler = new RegisterHandler(userManager.Object, emailService.Object);
        var command = BuildCommand(new List<string> { requestedLabel });

        await handler.Handle(command, CancellationToken.None);

        persistedUser.Should().NotBeNull();
        persistedUser!.GetType().Should().Be(typeof(User));
        persistedRoles.Should().ContainSingle().Which.Should().Be(RoleCodes.Prospect);
    }

    [Fact]
    public async Task Handle_EmptyRolesList_PersistsProspect()
    {
        // §6.2 — the real public sign-up form has no role selector and sends
        // Roles: [] (see authApi.register in the frontend). An empty list is
        // the normal shape of this request, not invalid input: it must
        // succeed and still produce PROSPECT, exactly like any other request
        // that clears the internal-role/BUYER guards.
        User? persistedUser = null;
        IEnumerable<string>? persistedRoles = null;

        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .Callback<User, string>((u, _) => persistedUser = u)
            .ReturnsAsync(IdentityResult.Success);
        userManager
            .Setup(m => m.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()))
            .Callback<User, IEnumerable<string>>((_, roles) => persistedRoles = roles)
            .ReturnsAsync(IdentityResult.Success);

        var emailService = new Mock<IEmailService>();
        var handler = new RegisterHandler(userManager.Object, emailService.Object);
        var command = BuildCommand(new List<string>());

        await handler.Handle(command, CancellationToken.None);

        persistedUser.Should().NotBeNull();
        persistedUser!.GetType().Should().Be(typeof(User));
        persistedRoles.Should().ContainSingle().Which.Should().Be(RoleCodes.Prospect);
    }
}
