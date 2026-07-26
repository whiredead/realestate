using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Construction.Entities;
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
    public DbSet<ImmeubleAssignment> Assignments { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<AppointmentReview> AppointmentReviews { get; set; }
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<User>(b =>
        {
            b.ToTable("AspNetUsers");
            b.HasDiscriminator<string>("Discriminator")
            .HasValue<User>("User")
            .HasValue<User>("Acheteur")  // Buyers stored as base User type
            .HasValue<Agent>("Agent")
            .HasValue<Notary>("Notaire"); // Notaries mapped to Notary entity

        });
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
        builder.ApplyConfiguration(new FeedbackConfiguration()); 
        builder.ApplyConfiguration(new AppointmentConfiguration());
        builder.ApplyConfiguration(new AppointmentReviewConfiguration());
        builder.ApplyConfiguration(new IncidentConfiguration());
        builder.ApplyConfiguration(new NotaryAppointmentConfiguration());
        builder.ApplyConfiguration(new PropertyDeliveryConfiguration());
        builder.ApplyConfiguration(new AssignmentConfiguration());
        builder.ApplyConfiguration(new LikedProjectsConfiguration());
        builder.ApplyConfiguration(new ImmeubleTrackingConfiguration());
        builder.ApplyConfiguration(new ReservationConfiguration());
        builder.ApplyConfiguration(new LeadConfiguration());
        builder.ApplyConfiguration(new TypeBienConfiguration());
        builder.ApplyConfiguration(new ProjectTypeBienConfiguration());
        builder.ApplyConfiguration(new QuartierConfiguration());
        builder.ApplyConfiguration(new PurchaseConfiguration());
        builder.ApplyConfiguration(new ProjectAssignmentConfiguration()); 
        builder.ApplyConfiguration(new WeeklyAvailabilityConfiguration());
        builder.ApplyConfiguration(new NotaryBlockConfiguration());
        builder.ApplyConfiguration(new ReservationDocumentConfiguration());
        builder.ApplyConfiguration(new AfterSaleClaimConfiguration());
        builder.ApplyConfiguration(new ClaimAttachmentConfiguration());
        builder.ApplyConfiguration(new ClaimCommentConfiguration());
        builder.ApplyConfiguration(new ClaimHistoryConfiguration());
        builder.ApplyConfiguration(new SaleConfiguration());
    }
    /// <summary>
    /// Constructor for ApplicationDbContext.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
}