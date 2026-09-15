using Moq;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Api.Application.Internal.ProvisionInternalUser;
using ProjectAPI.Domain.Identity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;

namespace Men.ProfessorAssignmentApi.Api.Tests.Unit;

/// <summary>
/// Phase 2 regression tests for ProvisionInternalUserHandler — the only
/// place besides RegisterHandler allowed to create an account or grant an
/// internal role, reachable only through the internal API-key-protected
/// endpoint. Covers the three required branches: new account, existing
/// account gaining a new role, and existing account that already holds the
/// requested role (no-op, no duplicate AddToRoleAsync call).
/// </summary>
public class ProvisionInternalUserHandlerTests
{
    private static Mock<RoleManager<Role>> CreateRoleManagerMock(bool roleExists = true)
    {
        var store = new Mock<IRoleStore<Role>>();
        var roleManager = new Mock<RoleManager<Role>>(store.Object, null!, null!, null!, null!);
        roleManager.Setup(m => m.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(roleExists);
        roleManager.Setup(m => m.CreateAsync(It.IsAny<Role>())).ReturnsAsync(IdentityResult.Success);
        return roleManager;
    }

    private static ProvisionInternalUserCommand BuildCommand(string email, string roleCode) => new()
    {
        Email = email,
        FirstName = "Jane",
        LastName = "Doe",
        Password = "P@ssw0rd!",
        RoleCode = roleCode
    };

    [Fact]
    public async Task Handle_NoExistingAccount_CreatesUserAndGrantsRole()
    {
        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        User? createdUser = null;
        userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .Callback<User, string>((u, _) => createdUser = u)
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<User>())).ReturnsAsync(new List<string>());
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var roleManager = CreateRoleManagerMock();
        var handler = new ProvisionInternalUserHandler(userManager.Object, roleManager.Object);

        var response = await handler.Handle(BuildCommand("new.agent@test.local", "SALES_AGENT"), CancellationToken.None);

        response.WasExistingAccount.Should().BeFalse();
        createdUser.Should().NotBeNull();
        userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), "P@ssw0rd!"), Times.Once);
        userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), "SALES_AGENT"), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingAccountWithoutTheRole_AddsRoleAndDoesNotCreateOrTouchPassword()
    {
        var existingUser = new User
        {
            Id = "existing-1",
            UserName = "prospect@test.local",
            Email = "prospect@test.local",
            FirstName = "Prospect",
            LastName = "User"
        };

        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager.Setup(m => m.FindByEmailAsync("prospect@test.local")).ReturnsAsync(existingUser);
        userManager.Setup(m => m.GetRolesAsync(existingUser)).ReturnsAsync(new List<string> { "PROSPECT" });
        userManager.Setup(m => m.AddToRoleAsync(existingUser, "NOTARY")).ReturnsAsync(IdentityResult.Success);

        var roleManager = CreateRoleManagerMock();
        var handler = new ProvisionInternalUserHandler(userManager.Object, roleManager.Object);

        var response = await handler.Handle(BuildCommand("prospect@test.local", "NOTARY"), CancellationToken.None);

        response.WasExistingAccount.Should().BeTrue("the account already existed as PROSPECT — it must be reused, not duplicated");
        userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never,
            "an existing account must never be re-created");
        userManager.Verify(m => m.AddToRoleAsync(existingUser, "NOTARY"), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingAccountAlreadyHoldingTheRole_IsANoOp()
    {
        var existingUser = new User
        {
            Id = "existing-2",
            UserName = "agent@test.local",
            Email = "agent@test.local",
            FirstName = "Existing",
            LastName = "Agent"
        };

        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager.Setup(m => m.FindByEmailAsync("agent@test.local")).ReturnsAsync(existingUser);
        userManager.Setup(m => m.GetRolesAsync(existingUser)).ReturnsAsync(new List<string> { "SALES_AGENT" });

        var roleManager = CreateRoleManagerMock();
        var handler = new ProvisionInternalUserHandler(userManager.Object, roleManager.Object);

        var response = await handler.Handle(BuildCommand("agent@test.local", "SALES_AGENT"), CancellationToken.None);

        response.WasExistingAccount.Should().BeTrue();
        response.RolesAfter.Should().Contain("SALES_AGENT");
        userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never,
            "the account already holds the role — re-adding it would be a redundant write, not idempotent no-op behavior");
    }
}
