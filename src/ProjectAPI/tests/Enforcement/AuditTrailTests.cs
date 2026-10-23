using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Audit;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Auditing;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §7 audit trail — the parts that decide WHAT is recorded. No database is needed:
/// the policy is pure and the collector only reads the change tracker.
/// </summary>
public class AuditTrailTests
{
    private static readonly Type[] RequestTypes = typeof(AuditPolicy).Assembly.GetTypes()
        .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(MediatR.IRequest<>)))
        .ToArray();

    // ------------------------------------------------------------ policy

    [Fact]
    public void Every_name_in_the_policy_is_a_real_request()
    {
        var names = RequestTypes.Select(t => t.Name).ToHashSet();
        AuditPolicy.Excluded.Concat(AuditPolicy.ForceAudited).Concat(AuditPolicy.Security).Concat(AuditPolicy.SecurityFailuresOnly).Concat(AuditPolicy.Access.Keys)
            .Should().OnlyContain(n => names.Contains(n), "a renamed command would silently change what the audit records");
    }

    [Theory]
    [InlineData("ApproveReservationCommand", true)]
    [InlineData("LockoutUserQuery", true)]
    [InlineData("GetReferenceItemsQuery", false)]
    [InlineData("MarkNotificationReadCommand", false)]
    public void Commands_are_audited_queries_are_not(string typeName, bool audited) =>
        AuditPolicy.IsAudited(RequestTypes.Single(t => t.Name == typeName)).Should().Be(audited);

    [Theory]
    [InlineData("ApproveReservationCommand", "APPROVE_RESERVATION")]
    [InlineData("SetUserPermissionCommand", "SET_USER_PERMISSION")]
    [InlineData("LockoutUserQuery", "LOCKOUT_USER")]
    public void Action_codes_are_upper_snake_case(string typeName, string expected) =>
        AuditPolicy.ActionFor(RequestTypes.Single(t => t.Name == typeName)).Should().Be(expected);

    [Fact]
    public void The_journal_query_has_no_property_the_actor_stamping_would_overwrite()
    {
        // ActorStampingBehaviour rewrites these names with the caller's id; a journal filter
        // named like that silently showed the viewer only their own actions.
        var stamped = new[] { "ActorUserId", "AuthorUserId", "CreatedBy", "ConnectedUserId", "UpdatedByUserId", "BuyerUserId" };
        typeof(ProjectAPI.Api.Application.AuditLogs.GetAuditLogsQuery).GetProperties().Select(p => p.Name)
            .Should().NotIntersectWith(stamped);
    }

    [Fact]
    public void A_refused_login_reports_a_failure_to_the_audit()
    {
        // Login answers 200 with IsAutheticated = false instead of throwing.
        IAuditOutcome refused = new ProjectAPI.Api.Application.Identity.Users.Login.LoginResponse { IsAutheticated = false, Message = "Invalid username or password" };
        IAuditOutcome accepted = new ProjectAPI.Api.Application.Identity.Users.Login.LoginResponse { IsAutheticated = true, Message = "" };

        refused.Succeeded.Should().BeFalse();
        refused.FailureReason.Should().Be("Invalid username or password");
        accepted.Succeeded.Should().BeTrue();
    }

    // ------------------------------------------------------------ collector

    private static ApplicationDbContext Context() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused").Options);

    [Fact]
    public void A_modified_row_records_from_and_to_but_never_secrets()
    {
        using var db = Context();
        var user = new User { Id = "u1", FirstName = "Old", LastName = "Name", PasswordHash = "hash-1" };
        db.Attach(user);
        user.FirstName = "New";
        user.PasswordHash = "hash-2";

        var change = AuditChangeCollector.Describe(db.Entry(user));

        change.Entity.Should().Be("User");
        change.Op.Should().Be("Modified");
        change.Fields.Should().ContainKey("FirstName");
        change.Fields!["FirstName"].Should().Be(new AuditFieldChange("Old", "New"));
        change.Fields.Should().NotContainKey("PasswordHash");
    }

    [Fact]
    public void A_row_is_a_subject_together_with_the_records_it_points_to()
    {
        using var db = Context();
        var unitId = Guid.NewGuid();
        var reservation = new Reservation { Id = Guid.NewGuid(), UnitId = unitId };
        db.Add(reservation);

        var subjects = AuditChangeCollector.SubjectsOf(db.Entry(reservation)).ToList();

        subjects.Should().Contain(("Reservation", reservation.Id.ToString()));
        subjects.Should().Contain(("Unit", unitId.ToString()));
    }
}
