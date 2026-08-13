using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Common.Idempotency;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Imports.Entities;
using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Configurations;

namespace ProjectAPI.Infrastructure.Context;

/// <summary>
/// Represents the application database context, extending IdentityDbContext for Project management.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<User>
{
    public DbSet<Immeuble> Immeubles { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<Floor> Floors { get; set; }
    public DbSet<ImmeubleAssignment> Assignments { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<AgentBlock> AgentBlocks { get; set; }
    public DbSet<AgentWeeklyAvailability> AgentWeeklyAvailabilities { get; set; }
    public DbSet<AgentDateOverride> AgentDateOverrides { get; set; }
    public DbSet<AgentAppointmentSettings> AgentAppointmentSettings { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<AppointmentReview> AppointmentReviews { get; set; }
    public DbSet<AppointmentAssignmentHistory> AppointmentAssignmentHistories { get; set; }
    public DbSet<AppointmentVisitReport> AppointmentVisitReports { get; set; }
    public DbSet<ProjectAgentAssignmentConfig> ProjectAgentAssignmentConfigs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<LikedProject> LikedProjects { get; set; }
    public DbSet<TypeBien> TypeBiens { get; set; }
    public DbSet<ProjectTypeBien> ProjectTypeBiens { get; set; }
    public DbSet<QuartierAmenity> QuartierAmenities { get; set; }
    
    // Reservations and Documents
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<ReservationDocument> ReservationDocuments { get; set; }
    
    // Purchases
    public DbSet<Purchase> Purchases { get; set; }
    
    // After-Sales Claims
    public DbSet<AfterSaleClaim> AfterSaleClaims { get; set; }
    public DbSet<ClaimAttachment> ClaimAttachments { get; set; }
    public DbSet<ClaimComment> ClaimComments { get; set; }
    public DbSet<ClaimHistory> ClaimHistories { get; set; }

    // Payment schedules and financial entries (spec §14, §48.6)
    public DbSet<PaymentSchedule> PaymentSchedules { get; set; }
    public DbSet<PaymentInstallment> PaymentInstallments { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentAllocation> PaymentAllocations { get; set; }

    // Construction tracking and land title (spec §15, §16, §48.7)
    public DbSet<ConstructionMilestone> ConstructionMilestones { get; set; }
    public DbSet<ConstructionUpdate> ConstructionUpdates { get; set; }
    public DbSet<UnitTitleState> UnitTitleStates { get; set; }
    public DbSet<UnitTitleHistory> UnitTitleHistories { get; set; }

    /// <summary>Append-only unit commercial-status trail (spec §3, §7).</summary>
    public DbSet<UnitStatusHistory> UnitStatusHistories { get; set; }

    // Final visit and snags (spec §17, §48.8)
    public DbSet<FinalVisitCase> FinalVisitCases { get; set; }
    public DbSet<FinalVisitAppointment> FinalVisitAppointments { get; set; }
    public DbSet<FinalVisitReport> FinalVisitReports { get; set; }
    public DbSet<Snag> Snags { get; set; }
    public DbSet<SnagHistory> SnagHistories { get; set; }

    // Handover and warranty (spec §5.8, §19, §48.9)
    public DbSet<ProjectAPI.Domain.Handovers.Entities.HandoverAppointment> HandoverAppointments { get; set; }
    public DbSet<ProjectAPI.Domain.Handovers.Entities.HandoverReport> HandoverReports { get; set; }
    public DbSet<ProjectAPI.Domain.Handovers.Entities.HandoverItem> HandoverItems { get; set; }
    public DbSet<ProjectAPI.Domain.Handovers.Entities.Warranty> Warranties { get; set; }

    /// <summary>§1.1 — people the business knows, with or without a login.</summary>
    public DbSet<ProjectAPI.Domain.Crm.Entities.CrmContact> CrmContacts { get; set; }

    /// <summary>§1.1/§6.2 — pending invitations for an approved buyer with no account yet.</summary>
    public DbSet<ProjectAPI.Domain.Crm.Entities.AccountInvitation> AccountInvitations { get; set; }

    /// <summary>Phase 2 — invitations to internal roles (SALES_AGENT/TECHNICIAN/NOTARY/PROJECT_ADMIN/GLOBAL_ADMIN). Separate from AccountInvitations (buyer-only).</summary>
    public DbSet<ProjectAPI.Domain.Invitations.Entities.InternalInvitation> InternalInvitations { get; set; }
    public DbSet<ProjectAPI.Domain.Invitations.Entities.InternalInvitationProjectAssignment> InternalInvitationProjectAssignments { get; set; }

    /// <summary>§7 — one row per (operation, caller-supplied key), backing IdempotencyBehaviour.</summary>
    public DbSet<IdempotencyKeyRecord> IdempotencyKeys { get; set; }

    /// <summary>§5.11, §23 — Excel stock import batches and their row-level validation results.</summary>
    public DbSet<ImportBatch> ImportBatches { get; set; }
    public DbSet<ImportRow> ImportRows { get; set; }

    /// <summary>§6.2 — per-user transactional notifications.</summary>
    public DbSet<Notification> Notifications { get; set; }

    /// <summary>§7 — dedup marker so scheduled jobs don't re-send the same reminder every pass.</summary>
    public DbSet<SentReminder> SentReminders { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<User>(b =>
        {
            b.ToTable("AspNetUsers");
            // TPH discriminator. EF registers exactly ONE value per CLR type, so
            // the previous pair of `HasValue<User>` calls ("User" then "Acheteur")
            // did not register two aliases — the second replaced the first, and
            // every row stored as 'User' then failed to materialise with
            // "No discriminators matched the discriminator value 'User'".
            //
            // 'User' is the base type's value. A buyer is a plain User: what makes
            // someone a buyer is an approved reservation and the BUYER role
            // (§6.2), not an inheritance branch. Legacy 'Acheteur' rows are
            // migrated to 'User' by AlignUserDiscriminator.
            b.HasDiscriminator<string>("Discriminator")
            .HasValue<User>("User")
            .HasValue<Agent>("Agent")
            .HasValue<Notary>("Notaire"); // Notaries mapped to Notary entity

        });
        builder.ApplyConfiguration(new CrmContactConfiguration());
        builder.ApplyConfiguration(new HandoverAppointmentConfiguration());
        builder.ApplyConfiguration(new HandoverReportConfiguration());
        builder.ApplyConfiguration(new HandoverItemConfiguration());
        builder.ApplyConfiguration(new WarrantyConfiguration());
        builder.ApplyConfiguration(new QuartierAmenityConfiguration());
        builder.ApplyConfiguration(new PaymentScheduleConfiguration());
        builder.ApplyConfiguration(new PaymentInstallmentConfiguration());
        builder.ApplyConfiguration(new PaymentConfiguration());
        builder.ApplyConfiguration(new PaymentAllocationConfiguration());
        builder.ApplyConfiguration(new ConstructionMilestoneConfiguration());
        builder.ApplyConfiguration(new ConstructionUpdateConfiguration());
        builder.ApplyConfiguration(new UnitTitleStateConfiguration());
        builder.ApplyConfiguration(new UnitTitleHistoryConfiguration());
        builder.ApplyConfiguration(new FinalVisitCaseConfiguration());
        builder.ApplyConfiguration(new FinalVisitAppointmentConfiguration());
        builder.ApplyConfiguration(new FinalVisitReportConfiguration());
        builder.ApplyConfiguration(new SnagConfiguration());
        builder.ApplyConfiguration(new SnagHistoryConfiguration());
        builder.ApplyConfiguration(new ProjectConfiguration());
        builder.ApplyConfiguration(new ImmeubleConfiguration());
        builder.ApplyConfiguration(new ImmeublePlanInterieurConfiguration());
        builder.ApplyConfiguration(new AppointmentConfiguration());
        builder.ApplyConfiguration(new PerformanceIndicatorConfiguration());
        builder.ApplyConfiguration(new UnitConfiguration());
        builder.ApplyConfiguration(new UnitStatusHistoryConfiguration());
        builder.ApplyConfiguration(new FloorConfiguration());
        builder.ApplyConfiguration(new FeedbackConfiguration()); 
        builder.ApplyConfiguration(new AppointmentConfiguration());
        builder.ApplyConfiguration(new AppointmentReviewConfiguration());
        builder.ApplyConfiguration(new IncidentConfiguration());
        builder.ApplyConfiguration(new NotaryAppointmentConfiguration());
        builder.ApplyConfiguration(new NotaryAppointmentAssignmentHistoryConfiguration());
        builder.ApplyConfiguration(new PropertyDeliveryConfiguration());
        builder.ApplyConfiguration(new AssignmentConfiguration());
        builder.ApplyConfiguration(new LikedProjectsConfiguration());
        builder.ApplyConfiguration(new ImmeubleTrackingConfiguration());
        builder.ApplyConfiguration(new ReservationConfiguration());
        builder.ApplyConfiguration(new ReservationBuyerConfiguration());
        builder.ApplyConfiguration(new LeadConfiguration());
        builder.ApplyConfiguration(new TypeBienConfiguration());
        builder.ApplyConfiguration(new ProjectTypeBienConfiguration());
        builder.ApplyConfiguration(new QuartierConfiguration());
        builder.ApplyConfiguration(new PurchaseConfiguration());
        builder.ApplyConfiguration(new ProjectAssignmentConfiguration());
        builder.ApplyConfiguration(new ProjectMembershipConfiguration());
        builder.ApplyConfiguration(new ProjectAgentAssignmentConfigConfiguration());
        builder.ApplyConfiguration(new AppointmentAssignmentHistoryConfiguration());
        builder.ApplyConfiguration(new AppointmentVisitReportConfiguration());
        builder.ApplyConfiguration(new WeeklyAvailabilityConfiguration());
        builder.ApplyConfiguration(new NotaryBlockConfiguration());
        builder.ApplyConfiguration(new AgentWeeklyAvailabilityConfiguration());
        builder.ApplyConfiguration(new AgentBlockConfiguration());
        builder.ApplyConfiguration(new AgentDateOverrideConfiguration());
        builder.ApplyConfiguration(new AgentAppointmentSettingsConfiguration());
        builder.ApplyConfiguration(new ReservationDocumentConfiguration());
        builder.ApplyConfiguration(new AfterSaleClaimConfiguration());
        builder.ApplyConfiguration(new ClaimAttachmentConfiguration());
        builder.ApplyConfiguration(new ClaimCommentConfiguration());
        builder.ApplyConfiguration(new ClaimHistoryConfiguration());
        builder.ApplyConfiguration(new SaleConfiguration());
        builder.ApplyConfiguration(new IdempotencyKeyRecordConfiguration());
        builder.ApplyConfiguration(new ImportBatchConfiguration());
        builder.ApplyConfiguration(new ImportRowConfiguration());
        builder.ApplyConfiguration(new NotificationConfiguration());
        builder.ApplyConfiguration(new SentReminderConfiguration());
        builder.ApplyConfiguration(new AccountInvitationConfiguration());
        builder.ApplyConfiguration(new InternalInvitationConfiguration());
        builder.ApplyConfiguration(new InternalInvitationProjectAssignmentConfiguration());
    }
    /// <summary>
    /// Constructor for ApplicationDbContext.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
}