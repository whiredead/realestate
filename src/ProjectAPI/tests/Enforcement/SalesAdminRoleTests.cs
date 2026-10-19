using FluentAssertions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// SALES_ADMIN ("Admin des ventes"): who decides on a reservation, and the
/// contract document types only the contract step may write.
/// </summary>
public class SalesAdminRoleTests
{
    private static string[] Split(string csv) => csv.Split(',', StringSplitOptions.TrimEntries);

    [Fact]
    public void Reservation_decisions_belong_to_the_sales_admin_with_the_project_admin_as_fallback()
    {
        Split(RoleGroups.ReservationDeciders).Should().BeEquivalentTo(RoleCodes.SalesAdmin, RoleCodes.ProjectAdmin);
    }

    [Fact]
    public void Sales_admin_keeps_the_ledger_and_reads_reservation_files()
    {
        Split(RoleGroups.PaymentManagers).Should().Contain(RoleCodes.SalesAdmin);
        Split(RoleGroups.ReservationReaders).Should().Contain(RoleCodes.SalesAdmin);
    }

    [Fact]
    public void Sales_admin_is_an_internal_project_scoped_role()
    {
        RoleCodes.Internal.Should().Contain(RoleCodes.SalesAdmin);
        RoleCodes.MembershipRoles.Should().Contain(RoleCodes.SalesAdmin);
        RoleCodes.Normalize("sales_admin").Should().Be(RoleCodes.SalesAdmin);
    }

    [Theory]
    [InlineData("SALES_CONTRACT", true)]
    [InlineData("RESERVATION_CONTRACT", false)] // the agent-side checklist piece, not the contract step
    [InlineData(" sales_contract_signed ", true)]
    [InlineData("CONTRACT", false)]
    [InlineData("CIN", false)]
    [InlineData(null, false)]
    public void Contract_document_types_are_reserved(string? documentType, bool reserved)
    {
        ReservationContractDocumentTypes.IsReserved(documentType).Should().Be(reserved);
    }
}
