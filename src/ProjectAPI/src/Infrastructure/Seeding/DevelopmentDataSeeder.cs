using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Seeding;

/// <summary>
/// Seeds the reference and workflow data a usable environment needs (spec §6.1,
/// §14, §15, §16, §17).
///
/// Two things make this safe to run on every startup:
///
///   * Every statement is guarded by a NOT EXISTS / MERGE, so a second pass is a
///     no-op. The seeder is idempotent, not "run once and remember".
///   * Nothing is ever UPDATEd or DELETEd. It only fills gaps, so it can never
///     overwrite data someone entered by hand.
///
/// The generated rows are DERIVED from the reservations and units already in the
/// database rather than invented: contract amounts come from each reservation's
/// own price, and payment coverage follows its status. That is what keeps the
/// seeded ledger internally consistent — installments sum to the contract amount,
/// allocations never exceed their payment, milestone weights sum to 100.
///
/// Roles are the one part that is not optional in practice: <c>AspNetRoles</c>
/// being empty means no user can be authorised at all, so an unseeded database
/// looks like a permissions bug rather than missing data.
/// </summary>
public class DevelopmentDataSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DevelopmentDataSeeder> _logger;

    public DevelopmentDataSeeder(ApplicationDbContext db, ILogger<DevelopmentDataSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Runs every seed step in a single transaction: the environment ends up
    /// either fully seeded or entirely untouched, never half-populated.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!await _db.Database.CanConnectAsync(ct))
        {
            _logger.LogWarning("[Seed] Database unreachable; skipping.");
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var (name, sql) in Steps)
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(sql, ct);
            if (affected > 0)
            {
                _logger.LogInformation("[Seed] {Step}: {Count} row(s) inserted.", name, affected);
            }
        }

        await tx.CommitAsync(ct);
        _logger.LogInformation("[Seed] Completed.");
    }

    /// <summary>
    /// Steps in dependency order: a step may rely on rows an earlier one created
    /// (allocations need payments, snags need reports, and so on).
    /// </summary>
    private static readonly (string Name, string Sql)[] Steps =
    {
        ("Roles", RolesSql),
        ("UserRoles", UserRolesSql),
        ("PaymentSchedules", PaymentSchedulesSql),
        ("PaymentInstallments", PaymentInstallmentsSql),
        ("Payments", PaymentsSql),
        ("PaymentAllocations", PaymentAllocationsSql),
        ("ConstructionMilestones", ConstructionMilestonesSql),
        ("ConstructionUpdates", ConstructionUpdatesSql),
        ("UnitTitleStates", UnitTitleStatesSql),
        ("UnitTitleHistories", UnitTitleHistoriesSql),
        ("FinalVisitCases", FinalVisitCasesSql),
        ("FinalVisitAppointments", FinalVisitAppointmentsSql),
        ("FinalVisitReports", FinalVisitReportsSql),
        ("Snags", SnagsSql),
        ("SnagHistories", SnagHistoriesSql)
    };

    // Status literals below are the numeric values of the domain enums:
    //   ReservationStatus        Pending0 Approved1 Rejected2 Cancelled3 Sold4
    //   PaymentScheduleStatus    Draft0 Active1 Superseded2 Cancelled3
    //   PaymentStatus            PendingValidation0 Validated1 Rejected2 Reversed3
    //   MilestoneStatus          NotStarted0 InProgress1 Completed2 Delayed3
    //   TitleStatus              NotAvailable0 InProgress1 Available2 DeliveredToNotary3 Completed4
    //   FinalVisitCaseStatus     Open0 RevisitRequired1 ReadyForNotary2 Closed3
    //   AppointmentAttemptStatus Requested0 Confirmed1 ... Completed5 NoShow6
    //   ReportStatus             Draft0 Submitted1 AwaitingBuyerAck2 Acknowledged3
    //   VisitResult              CompliantNoSnag0 CompliantMinorSnags1 ...
    //   SnagSeverity             Minor0 Major1 Blocking2
    //   SnagStatus               Open0 Acknowledged1 InResolution2 Resolved3 Validated4 Closed5
    //
    // Units.Status is a STRING in the canonical UPPER_SNAKE_CASE vocabulary
    // ('AVAILABLE', 'RESERVED', 'SOLD', …) enforced by CK_Units_Status since
    // AddUnitCommercialStatusMachine.

    /// <summary>The seven storable role codes of §6.1 (VISITOR is never stored).</summary>
    private const string RolesSql = """
        MERGE AspNetRoles AS t
        USING (VALUES
            ('PROSPECT'), ('BUYER'), ('SALES_AGENT'), ('TECHNICIAN'),
            ('NOTARY'), ('PROJECT_ADMIN'), ('GLOBAL_ADMIN')
        ) AS s(Name) ON t.NormalizedName = s.Name
        WHEN NOT MATCHED THEN
            INSERT (Id, Name, NormalizedName, ConcurrencyStamp)
            VALUES (CONVERT(varchar(36), NEWID()), s.Name, s.Name, CONVERT(varchar(36), NEWID()));
        """;

    /// <summary>
    /// Maps each account onto a role using exactly the translation
    /// <c>RoleCodes.Normalize()</c> applies to the legacy French labels, so the
    /// stored roles agree with what authorisation computes at runtime.
    /// Anything unrecognised falls to PROSPECT — failing closed, never open.
    /// </summary>
    private const string UserRolesSql = """
        INSERT INTO AspNetUserRoles (UserId, RoleId)
        SELECT u.Id, r.Id
        FROM AspNetUsers u
        JOIN AspNetRoles r
          ON r.NormalizedName = CASE u.Discriminator
                WHEN 'Admin'    THEN 'GLOBAL_ADMIN'
                WHEN 'Agent'    THEN 'SALES_AGENT'
                WHEN 'Notaire'  THEN 'NOTARY'
                WHEN 'Acheteur' THEN 'BUYER'
                ELSE 'PROSPECT' END
        WHERE NOT EXISTS (
            SELECT 1 FROM AspNetUserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);
        """;

    /// <summary>
    /// One ACTIVE schedule per Approved(1)/Sold(4) reservation, carrying that
    /// reservation's own price as the contract amount (§14.2).
    /// </summary>
    private const string PaymentSchedulesSql = """
        INSERT INTO PaymentSchedules
            (Id, ReservationId, VersionNo, Status, ContractAmount, Currency, ActivatedAt, SupersedesId, CreatedAt)
        SELECT NEWID(), r.Id, 1, 1, r.TotalPropertyPrice, 'MAD',
               DATEADD(day, 2, r.ReservationDate), NULL, DATEADD(day, 1, r.ReservationDate)
        FROM Reservations r
        WHERE r.Status IN (1, 4)
          AND NOT EXISTS (SELECT 1 FROM PaymentSchedules ps WHERE ps.ReservationId = r.Id);
        """;

    /// <summary>
    /// The standard Moroccan VEFA split: 20% booking / 30% structure /
    /// 30% finishing / 20% handover. Percentages total 100 and the amounts total
    /// the contract amount, which §14.2 requires.
    /// </summary>
    private const string PaymentInstallmentsSql = """
        INSERT INTO PaymentInstallments
            (Id, ScheduleId, SequenceNo, LabelFr, LabelEn, Percentage, Amount, DueDate, IsCancelled, Comment)
        SELECT NEWID(), ps.Id, v.SequenceNo, v.LabelFr, v.LabelEn, v.Pct,
               CAST(ps.ContractAmount * v.Pct / 100.0 AS decimal(15,2)),
               DATEADD(month, v.MonthOffset, r.ReservationDate), 0, NULL
        FROM PaymentSchedules ps
        JOIN Reservations r ON r.Id = ps.ReservationId
        CROSS APPLY (VALUES
            (1, N'Réservation',     N'Booking',          CAST(20.0000 AS decimal(7,4)), 0),
            (2, N'Gros œuvre',      N'Structural works', CAST(30.0000 AS decimal(7,4)), 3),
            (3, N'Finitions',       N'Finishing works',  CAST(30.0000 AS decimal(7,4)), 6),
            (4, N'Remise des clés', N'Handover',         CAST(20.0000 AS decimal(7,4)), 9)
        ) AS v(SequenceNo, LabelFr, LabelEn, Pct, MonthOffset)
        WHERE NOT EXISTS (
            SELECT 1 FROM PaymentInstallments pi WHERE pi.ScheduleId = ps.Id AND pi.SequenceNo = v.SequenceNo);
        """;

    /// <summary>
    /// Validated entries covering the installments already reached: a Sold(4)
    /// file has paid through finishing (80%), an Approved(1) file only the
    /// booking (20%). All are Validated(1), the only status §14.3 counts toward
    /// buyer totals. The SEED- reference makes the step idempotent and lets
    /// allocations match a payment to its instalment exactly.
    /// </summary>
    private const string PaymentsSql = """
        INSERT INTO Payments
            (Id, ReservationId, PaymentDate, Amount, Currency, MethodCode, ExternalReference,
             Source, Status, ReversalOfPaymentId, ValidatedBy, ValidatedAt, Comment, CreatedBy, CreatedAt)
        SELECT NEWID(), r.Id, pi.DueDate, pi.Amount, 'MAD',
               CASE pi.SequenceNo WHEN 2 THEN 'CHEQUE' ELSE 'TRANSFER' END,
               CONCAT('SEED-', LEFT(REPLACE(CONVERT(varchar(36), r.Id), '-', ''), 8), '-', pi.SequenceNo),
               'WEB', 1, NULL, adm.Id, DATEADD(day, 1, pi.DueDate),
               N'Encaissement confirmé par l''administration.', adm.Id, pi.DueDate
        FROM PaymentInstallments pi
        JOIN PaymentSchedules ps ON ps.Id = pi.ScheduleId
        JOIN Reservations r      ON r.Id = ps.ReservationId
        CROSS JOIN (SELECT TOP 1 Id FROM AspNetUsers WHERE Discriminator = 'Admin' ORDER BY Email) AS adm
        WHERE pi.SequenceNo <= CASE WHEN r.Status = 4 THEN 3 ELSE 1 END
          AND NOT EXISTS (
              SELECT 1 FROM Payments p
              WHERE p.ReservationId = r.Id
                AND p.ExternalReference = CONCAT('SEED-', LEFT(REPLACE(CONVERT(varchar(36), r.Id), '-', ''), 8), '-', pi.SequenceNo));
        """;

    /// <summary>
    /// Links each seeded payment to the instalment it settles. The join is on the
    /// reference suffix, so a payment is never allocated to the wrong instalment.
    /// </summary>
    private const string PaymentAllocationsSql = """
        INSERT INTO PaymentAllocations (Id, PaymentId, InstallmentId, AllocatedAmount, CreatedAt)
        SELECT NEWID(), p.Id, pi.Id, p.Amount, p.CreatedAt
        FROM Payments p
        JOIN PaymentSchedules ps ON ps.ReservationId = p.ReservationId
        JOIN PaymentInstallments pi
          ON pi.ScheduleId = ps.Id
         AND p.ExternalReference = CONCAT('SEED-', LEFT(REPLACE(CONVERT(varchar(36), p.ReservationId), '-', ''), 8), '-', pi.SequenceNo)
        WHERE p.Status = 1
          AND NOT EXISTS (
              SELECT 1 FROM PaymentAllocations pa WHERE pa.PaymentId = p.Id AND pa.InstallmentId = pi.Id);
        """;

    /// <summary>
    /// Five weighted milestones per project, summing to exactly 100 as §48.7
    /// caps them. The first two are Completed and the third InProgress, giving
    /// each project a realistic mid-construction profile.
    /// </summary>
    private const string ConstructionMilestonesSql = """
        INSERT INTO ConstructionMilestones
            (Id, ProjectId, Code, NameFr, NameEn, DescriptionFr, DescriptionEn, SequenceNo,
             WeightPercent, PlannedDate, ActualDate, Status, VisibleToBuyer, VisibleToPublic, CreatedAt)
        SELECT NEWID(), p.Id, v.Code, v.NameFr, v.NameEn, v.DescFr, v.DescEn, v.SequenceNo,
               v.Weight,
               DATEADD(month, v.SequenceNo * 4, '2025-01-15'),
               CASE WHEN v.SequenceNo <= 2 THEN DATEADD(month, v.SequenceNo * 4, '2025-01-20') END,
               CASE WHEN v.SequenceNo <= 2 THEN 2 WHEN v.SequenceNo = 3 THEN 1 ELSE 0 END,
               1, CASE WHEN v.SequenceNo <= 3 THEN 1 ELSE 0 END, SYSUTCDATETIME()
        FROM Projects p
        CROSS APPLY (VALUES
            ('FOUNDATION', N'Fondations',      N'Foundations', N'Terrassement et fondations.',       N'Earthworks and foundations.',  1, CAST(20.00 AS decimal(5,2))),
            ('STRUCTURE',  N'Gros œuvre',      N'Structural',  N'Élévation de la structure.',        N'Structural frame.',            2, CAST(30.00 AS decimal(5,2))),
            ('ENVELOPE',   N'Clos et couvert', N'Envelope',    N'Façades, menuiseries, étanchéité.', N'Facades and weatherproofing.', 3, CAST(20.00 AS decimal(5,2))),
            ('FINISHING',  N'Finitions',       N'Finishing',   N'Revêtements et peintures.',         N'Coatings and paintwork.',      4, CAST(20.00 AS decimal(5,2))),
            ('HANDOVER',   N'Livraison',       N'Handover',    N'Réception et remise des clés.',     N'Acceptance and key handover.', 5, CAST(10.00 AS decimal(5,2)))
        ) AS v(Code, NameFr, NameEn, DescFr, DescEn, SequenceNo, Weight)
        WHERE NOT EXISTS (
            SELECT 1 FROM ConstructionMilestones cm WHERE cm.ProjectId = p.Id AND cm.Code = v.Code);
        """;

    /// <summary>
    /// A published progress note per project. ProgressPercent is computed from the
    /// milestones above (full weight when Completed, half when InProgress) rather
    /// than hardcoded, so the headline figure cannot drift from the detail.
    /// </summary>
    private const string ConstructionUpdatesSql = """
        INSERT INTO ConstructionUpdates
            (Id, ProjectId, VersionNo, ProgressPercent, TitleFr, TitleEn, DescriptionFr, DescriptionEn,
             MediaUrls, Visibility, SupersedesId, AuthorUserId, PublishedAt, CreatedAt)
        SELECT NEWID(), p.Id, 1,
               (SELECT CAST(SUM(CASE cm.Status WHEN 2 THEN cm.WeightPercent WHEN 1 THEN cm.WeightPercent / 2 ELSE 0 END) AS decimal(5,2))
                FROM ConstructionMilestones cm WHERE cm.ProjectId = p.Id),
               N'Avancement du chantier', N'Construction progress',
               N'Gros œuvre achevé, travaux de clos et couvert en cours.',
               N'Structural works complete, envelope works under way.',
               NULL, 1, NULL, p.AgentId, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM Projects p
        WHERE NOT EXISTS (SELECT 1 FROM ConstructionUpdates cu WHERE cu.ProjectId = p.Id);
        """;

    /// <summary>
    /// Exactly one current title row per unit (§16), positioned to match the
    /// unit's commercial state: sold/delivered units have reached the notary,
    /// reserved or contracted units are in progress, the rest have nothing yet.
    /// </summary>
    private const string UnitTitleStatesSql = """
        INSERT INTO UnitTitleStates (Id, UnitId, Status, StatusAt, DocumentUrl, UpdatedAt)
        SELECT NEWID(), u.Id,
               CASE
                   WHEN u.Status IN ('SOLD', 'DELIVERED')                THEN 3
                   WHEN u.Status IN ('RESERVED', 'CONTRACTED',
                                     'HOLD_PENDING_APPROVAL')            THEN 1
                   ELSE 0
               END,
               SYSUTCDATETIME(), NULL, SYSUTCDATETIME()
        FROM Units u
        WHERE NOT EXISTS (SELECT 1 FROM UnitTitleStates ts WHERE ts.UnitId = u.Id);
        """;

    /// <summary>
    /// Replays the forward path each unit actually walked to reach its current
    /// status, so the append-only history agrees with the current state and never
    /// contains a step the unit has not reached.
    /// </summary>
    private const string UnitTitleHistoriesSql = """
        INSERT INTO UnitTitleHistories (Id, UnitId, FromStatus, ToStatus, ActorUserId, OccurredAt, Reason, DocumentUrl)
        SELECT NEWID(), ts.UnitId, v.FromStatus, v.ToStatus, adm.Id,
               DATEADD(day, v.ToStatus * 10, '2026-01-05'), NULL, NULL
        FROM UnitTitleStates ts
        CROSS JOIN (SELECT TOP 1 Id FROM AspNetUsers WHERE Discriminator = 'Admin' ORDER BY Email) AS adm
        CROSS APPLY (VALUES (0, 1), (1, 2), (2, 3)) AS v(FromStatus, ToStatus)
        WHERE v.ToStatus <= ts.Status
          AND NOT EXISTS (
              SELECT 1 FROM UnitTitleHistories th WHERE th.UnitId = ts.UnitId AND th.ToStatus = v.ToStatus);
        """;

    /// <summary>One case per Sold(4) reservation — the files that reached pre-delivery (§17.1).</summary>
    private const string FinalVisitCasesSql = """
        INSERT INTO FinalVisitCases (Id, ReservationId, Status, ResponsibleSalesAgentId, OpenedAt, ClosedAt)
        SELECT NEWID(), r.Id, 0, r.AgentId, DATEADD(month, 8, r.ReservationDate), NULL
        FROM Reservations r
        WHERE r.Status = 4
          AND NOT EXISTS (SELECT 1 FROM FinalVisitCases fc WHERE fc.ReservationId = r.Id);
        """;

    /// <summary>A single completed attempt per case, on a half-open [start, end) interval (§10.1).</summary>
    private const string FinalVisitAppointmentsSql = """
        INSERT INTO FinalVisitAppointments
            (Id, CaseId, AttemptNo, StartsAt, EndsAt, Status, PreviousAppointmentId, CauseType, CauseDescription, CreatedAt)
        SELECT NEWID(), fc.Id, 1,
               DATEADD(hour, 10, CAST(CAST(DATEADD(day, 14, fc.OpenedAt) AS date) AS datetime2)),
               DATEADD(hour, 12, CAST(CAST(DATEADD(day, 14, fc.OpenedAt) AS date) AS datetime2)),
               5, NULL, NULL, NULL, fc.OpenedAt
        FROM FinalVisitCases fc
        WHERE NOT EXISTS (SELECT 1 FROM FinalVisitAppointments fa WHERE fa.CaseId = fc.Id AND fa.AttemptNo = 1);
        """;

    /// <summary>
    /// An acknowledged report per visit, recording minor reserves (§17.3).
    /// The acknowledging buyer is the reservation's own buyer.
    /// </summary>
    private const string FinalVisitReportsSql = """
        INSERT INTO FinalVisitReports
            (Id, AppointmentId, VersionNo, Status, ResultCode, GeneralCondition, Observations,
             SubmittedAt, AcknowledgedAt, AcknowledgedBy, DisputeReason, AuthorUserId, CreatedAt)
        SELECT NEWID(), fa.Id, 1, 3, 1,
               N'Bon état général.',
               N'Quelques réserves mineures relevées lors de la visite.',
               fa.EndsAt, DATEADD(day, 2, fa.EndsAt), r.BuyerId, NULL, fc.ResponsibleSalesAgentId, fa.EndsAt
        FROM FinalVisitAppointments fa
        JOIN FinalVisitCases fc ON fc.Id = fa.CaseId
        JOIN Reservations r     ON r.Id = fc.ReservationId
        WHERE NOT EXISTS (SELECT 1 FROM FinalVisitReports fr WHERE fr.AppointmentId = fa.Id AND fr.VersionNo = 1);
        """;

    /// <summary>
    /// Two Minor(0) reserves per report. Severity matters here: only minor snags
    /// are consistent with an Acknowledged report on a unit already delivered to
    /// the notary, since a major one would still block that step (§17.4).
    /// Snags belong to the SALES agent, never the after-sales technician.
    /// </summary>
    private const string SnagsSql = """
        INSERT INTO Snags
            (Id, ReportId, Code, CategoryCode, Severity, Description, Location,
             TargetResolutionDate, Status, ResponsibleSalesAgentId, ResolutionComment, ProofUrl, CreatedAt)
        SELECT NEWID(), fr.Id,
               CONCAT('SNG-', LEFT(REPLACE(CONVERT(varchar(36), fr.Id), '-', ''), 6), '-', v.Seq),
               v.CategoryCode, 0, v.Descr, v.Loc,
               DATEADD(day, 30, fr.SubmittedAt), v.Status, fr.AuthorUserId,
               v.ResolutionComment, NULL, fr.CreatedAt
        FROM FinalVisitReports fr
        CROSS APPLY (VALUES
            (1, 'PAINT',    N'Retouche de peinture nécessaire dans le séjour.',   N'Séjour',  4, N'Retouche effectuée et validée.'),
            (2, 'PLUMBING', N'Léger suintement au niveau du robinet de cuisine.', N'Cuisine', 2, NULL)
        ) AS v(Seq, CategoryCode, Descr, Loc, Status, ResolutionComment)
        WHERE NOT EXISTS (
            SELECT 1 FROM Snags s
            WHERE s.ReportId = fr.Id
              AND s.Code = CONCAT('SNG-', LEFT(REPLACE(CONVERT(varchar(36), fr.Id), '-', ''), 6), '-', v.Seq));
        """;

    /// <summary>
    /// Replays each snag's transitions along the path <c>SnagStateMachine</c>
    /// permits (Open → Acknowledged → InResolution → Resolved → Validated),
    /// stopping at the status the snag actually holds.
    /// </summary>
    private const string SnagHistoriesSql = """
        INSERT INTO SnagHistories (Id, SnagId, FromStatus, ToStatus, ActorUserId, OccurredAt, Comment)
        SELECT NEWID(), s.Id, v.FromStatus, v.ToStatus, s.ResponsibleSalesAgentId,
               DATEADD(day, v.ToStatus * 3, s.CreatedAt), NULL
        FROM Snags s
        CROSS APPLY (VALUES (NULL, 0), (0, 1), (1, 2), (2, 3), (3, 4)) AS v(FromStatus, ToStatus)
        WHERE v.ToStatus <= s.Status
          AND NOT EXISTS (
              SELECT 1 FROM SnagHistories sh WHERE sh.SnagId = s.Id AND sh.ToStatus = v.ToStatus);
        """;
}
