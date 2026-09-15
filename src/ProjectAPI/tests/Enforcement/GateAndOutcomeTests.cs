using FluentAssertions;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Construction.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Step 1 of work order #4: the final-visit gate.
///
/// <c>AllowsFinalVisit</c> is the only guard on RequestFinalVisitCommand, and it
/// used to return true for the legacy value "Available" — which only ever meant
/// "units are on sale", never "construction finished". Every legacy project
/// therefore passed the gate. A mapping must never manufacture a state that
/// unlocks a gate.
/// </summary>
public class ProjectStatusGateTests
{
    [Theory]
    [InlineData("Available")]
    [InlineData("AVAILABLE")]
    [InlineData("Sold")]
    [InlineData("SOLD")]
    public void Legacy_commercial_values_do_not_unlock_the_final_visit_gate(string legacy)
    {
        ProjectStatusCodes.Normalize(legacy).Should().Be(ProjectStatusCodes.SurPlan,
            "a commercial fact says nothing about whether the build finished");

        ProjectStatusCodes.AllowsFinalVisit(legacy).Should().BeFalse(
            "a buyer must not be able to request a final visit on an unbuilt project (§5.6)");
    }

    [Theory]
    [InlineData("ComingSoon")]
    [InlineData("CommingSoon")]   // the historical misspelling
    [InlineData("UnderConstruction")]
    [InlineData("DRAFT")]
    [InlineData("SUSPENDED")]
    [InlineData("ARCHIVED")]
    [InlineData("SUR_PLAN")]
    [InlineData("FINALISE")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("something unknown")]
    public void Nothing_else_unlocks_it_either(string? status) =>
        ProjectStatusCodes.AllowsFinalVisit(status).Should().BeFalse();

    [Fact]
    public void Only_a_genuinely_completed_project_unlocks_it()
    {
        ProjectStatusCodes.AllowsFinalVisit("EN_LIVRAISON").Should().BeTrue();
        ProjectStatusCodes.AllowsFinalVisit("COMPLETED").Should().BeTrue();

        // DELIVERED asserts the build finished and was handed over, unlike the
        // commercial values above.
        ProjectStatusCodes.AllowsFinalVisit("DELIVERED").Should().BeTrue();
    }

    [Fact]
    public void Unknown_values_map_to_SUR_PLAN() =>
        ProjectStatusCodes.Normalize("¯\\_(ツ)_/¯").Should().Be(ProjectStatusCodes.SurPlan);

    [Fact]
    public void Canonical_codes_round_trip()
    {
        ProjectStatusCodes.All.Should().Equal("SUR_PLAN", "EN_LIVRAISON", "FINALISE");
        foreach (var code in ProjectStatusCodes.All)
        {
            ProjectStatusCodes.Normalize(code).Should().Be(code);
        }

        // Former internal codes map onto the three statuses.
        ProjectStatusCodes.Normalize("DRAFT").Should().Be(ProjectStatusCodes.SurPlan);
        ProjectStatusCodes.Normalize("PLANNED").Should().Be(ProjectStatusCodes.SurPlan);
        ProjectStatusCodes.Normalize("IN_PROGRESS").Should().Be(ProjectStatusCodes.SurPlan);
        ProjectStatusCodes.Normalize("COMPLETED").Should().Be(ProjectStatusCodes.EnLivraison);
        ProjectStatusCodes.Normalize("ARCHIVED").Should().Be(ProjectStatusCodes.Finalise);
    }
}

/// <summary>
/// Step 4 of work order #4: the notary outcome vocabulary, kept distinct from
/// the appointment status. Only PURCHASE_COMPLETED converts a sale (§5.7).
/// </summary>
public class NotaryOutcomeTests
{
    [Fact]
    public void The_five_spec_outcomes_exist_and_round_trip()
    {
        NotaryOutcomeCodes.All.Should().BeEquivalentTo(new[]
        {
            "PURCHASE_COMPLETED", "INCOMPLETE_FILE", "BUYER_ABSENT", "POSTPONED", "NOT_COMPLETED_OTHER"
        });

        foreach (var outcome in Enum.GetValues<NotaryAppointmentOutcome>())
        {
            NotaryOutcomeCodes.Parse(outcome.ToCode()).Should().Be(outcome);
        }
    }

    [Fact]
    public void Unknown_outcome_codes_are_rejected_rather_than_defaulted()
    {
        NotaryOutcomeCodes.TryParse("NOT_A_REAL_OUTCOME", out _).Should().BeFalse();
        NotaryOutcomeCodes.TryParse(null, out _).Should().BeFalse();
        NotaryOutcomeCodes.TryParse("", out _).Should().BeFalse();

        var act = () => NotaryOutcomeCodes.Parse("NOT_A_REAL_OUTCOME");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Outcome_is_a_separate_axis_from_appointment_status()
    {
        // The point of §5.7: the same status can carry entirely different
        // business outcomes, so one cannot be derived from the other.
        var outcomeNames = NotaryOutcomeCodes.All;
        var statusNames = Enum.GetNames<ProjectAPI.Domain.FinalVisits.Entities.AppointmentAttemptStatus>();

        outcomeNames.Should().NotIntersectWith(statusNames);
    }
}
