using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuartierAmenity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConstructionMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameFr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionFr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    WeightPercent = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VisibleToBuyer = table.Column<bool>(type: "bit", nullable: false),
                    VisibleToPublic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionMilestones", x => x.Id);
                    table.CheckConstraint("CK_ConstructionMilestones_Weight", "[WeightPercent] >= 0 AND [WeightPercent] <= 100");
                });

            migrationBuilder.CreateTable(
                name: "ConstructionUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    TitleFr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionFr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    SupersedesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionUpdates", x => x.Id);
                    table.CheckConstraint("CK_ConstructionUpdates_Progress", "[ProgressPercent] >= 0 AND [ProgressPercent] <= 100");
                });

            migrationBuilder.CreateTable(
                name: "FinalVisitCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponsibleSalesAgentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalVisitCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinalVisitReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResultCode = table.Column<int>(type: "int", nullable: false),
                    GeneralCondition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Observations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisputeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AuthorUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalVisitReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    MethodCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReversalOfPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Payments_ReversalOfPaymentId",
                        column: x => x.ReversalOfPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ContractAmount = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersedesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuartierAmenities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuartierAmenities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTitleHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTitleHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTitleStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTitleStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinalVisitAppointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreviousAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CauseType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CauseDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalVisitAppointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalVisitAppointments_FinalVisitCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "FinalVisitCases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Snags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TargetResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponsibleSalesAgentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolutionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProofUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Snags_FinalVisitReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "FinalVisitReports",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    LabelFr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentInstallments", x => x.Id);
                    table.CheckConstraint("CK_PaymentInstallments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_PaymentInstallments_Percentage", "[Percentage] >= 0 AND [Percentage] <= 100");
                    table.ForeignKey(
                        name: "FK_PaymentInstallments_PaymentSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "PaymentSchedules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SnagHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnagHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SnagHistories_Snags_SnagId",
                        column: x => x.SnagId,
                        principalTable: "Snags",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_PaymentInstallments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "PaymentInstallments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionMilestones_ProjectCode",
                table: "ConstructionMilestones",
                columns: new[] { "ProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionUpdates_ProjectVersion",
                table: "ConstructionUpdates",
                columns: new[] { "ProjectId", "VersionNo" });

            migrationBuilder.CreateIndex(
                name: "IX_FinalVisitAppointments_CaseAttempt",
                table: "FinalVisitAppointments",
                columns: new[] { "CaseId", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinalVisitAppointments_Interval",
                table: "FinalVisitAppointments",
                columns: new[] { "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinalVisitCases_Reservation",
                table: "FinalVisitCases",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinalVisitReports_AppointmentVersion",
                table: "FinalVisitReports",
                columns: new[] { "AppointmentId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_Installment",
                table: "PaymentAllocations",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentInstallments_ScheduleSequence",
                table: "PaymentInstallments",
                columns: new[] { "ScheduleId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Reservation",
                table: "Payments",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversalOfPaymentId",
                table: "Payments",
                column: "ReversalOfPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SourceReference",
                table: "Payments",
                columns: new[] { "Source", "ExternalReference" },
                unique: true,
                filter: "[ExternalReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSchedules_ActivePerReservation",
                table: "PaymentSchedules",
                column: "ReservationId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSchedules_ReservationVersion",
                table: "PaymentSchedules",
                columns: new[] { "ReservationId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuartierAmenities_ProjectId",
                table: "QuartierAmenities",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SnagHistories_SnagDate",
                table: "SnagHistories",
                columns: new[] { "SnagId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Snags_Code",
                table: "Snags",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Snags_ReportSeverityStatus",
                table: "Snags",
                columns: new[] { "ReportId", "Severity", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitTitleHistories_UnitDate",
                table: "UnitTitleHistories",
                columns: new[] { "UnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitTitleStates_Unit",
                table: "UnitTitleStates",
                column: "UnitId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructionMilestones");

            migrationBuilder.DropTable(
                name: "ConstructionUpdates");

            migrationBuilder.DropTable(
                name: "FinalVisitAppointments");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "QuartierAmenities");

            migrationBuilder.DropTable(
                name: "SnagHistories");

            migrationBuilder.DropTable(
                name: "UnitTitleHistories");

            migrationBuilder.DropTable(
                name: "UnitTitleStates");

            migrationBuilder.DropTable(
                name: "FinalVisitCases");

            migrationBuilder.DropTable(
                name: "PaymentInstallments");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Snags");

            migrationBuilder.DropTable(
                name: "PaymentSchedules");

            migrationBuilder.DropTable(
                name: "FinalVisitReports");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Reservations");
        }
    }
}
