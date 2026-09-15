using Moq;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Api.Application.Identity.Users.Register;
using FluentAssertions;

namespace Men.ProfessorAssignmentApi.Api.Tests.Unit;

/// <summary>
/// §6.2 — the ValidationBehaviour pipeline runs RegisterValidator before
/// RegisterHandler ever sees the request, so a rule here that's too strict
/// rejects public sign-up before the handler's own PROSPECT-only guard runs.
/// This pinned down the real cause of "One or more validation errors
/// occurred" on every real /register submission: Roles required NotEmpty(),
/// but the public form (no role selector) always sends Roles: [].
/// </summary>
public class RegisterValidatorTests
{
    private static RegisterCommand BuildCommand(List<string>? roles = null) => new()
    {
        UserName = "jdoe",
        Password = "P@ssw0rd!",
        FirstName = "Jane",
        LastName = "Doe",
        PhoneNumber = "0600000000",
        Email = "jane.doe@example.com",
        Roles = roles ?? new List<string>(),
    };

    [Fact]
    public void EmptyRolesList_IsValid()
    {
        var validator = new RegisterValidator();
        var result = validator.Validate(BuildCommand(new List<string>()));
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(RegisterCommand.Roles));
    }

    [Fact]
    public void NullRolesList_IsInvalid()
    {
        var validator = new RegisterValidator();
        var command = BuildCommand() with { Roles = null! };
        var result = validator.Validate(command);
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterCommand.Roles));
    }

    [Fact]
    public void UnknownRoleInList_IsInvalid()
    {
        var validator = new RegisterValidator();
        var result = validator.Validate(BuildCommand(new List<string> { "NOT_A_REAL_ROLE" }));
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith(nameof(RegisterCommand.Roles)));
    }

    [Theory]
    [InlineData("0612345678")]
    [InlineData("0712345678")]
    public void MoroccanMobileNumber_IsValid(string phone)
    {
        var validator = new RegisterValidator();
        var result = validator.Validate(BuildCommand() with { PhoneNumber = phone });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(RegisterCommand.PhoneNumber));
    }

    [Theory]
    [InlineData("+212612345678")]
    [InlineData("06 12 34 56 78")]
    [InlineData("0512345678")]
    public void NonMatchingPhoneFormats_AreInvalid(string phone)
    {
        // Documents the exact formats the backend regex rejects today, so a
        // future relaxation of ^(06|07)\d{8}$ is a deliberate, visible change
        // rather than an accidental one.
        var validator = new RegisterValidator();
        var result = validator.Validate(BuildCommand() with { PhoneNumber = phone });
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterCommand.PhoneNumber));
    }
}
