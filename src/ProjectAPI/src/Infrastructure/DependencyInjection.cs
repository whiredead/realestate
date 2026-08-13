using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.FeedBacks.Interfaces;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Context;
using ProjectAPI.Infrastructure.Providers;
using ProjectAPI.Infrastructure.Seeding;
using ProjectAPI.Infrastructure.Settings;

namespace ProjectAPI.Infrastructure;
/// <summary>
/// Represents a static class for dependency injection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Extension method to register infrastructure services required by the application.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The <see cref="IConfiguration"/> instance representing the application's configuration.</param>
    /// <returns>The modified <see cref="IServiceCollection"/> instance with added services.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register JWT settings
        services.ConfigureSettings(configuration);

        // Register DbContexts
        services.ConfigureDbContexts(configuration);

        // Add identity to get user informations
        services.ConfigureIdentity();

        // Register custom services
        services.ConfigureCustomServices();
        return services;
    }
    #region Helpers

    private static void ConfigureSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = new JwtSettings();
        var otpSettings = new OtpSettings();

        configuration.Bind("JwtSettings", jwtSettings);
        configuration.Bind("OtpSettings", otpSettings);

        services
            .AddSingleton(jwtSettings)
            .AddSingleton(otpSettings);
    }
    private static void ConfigureIdentity(this IServiceCollection services)
    {
        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
    }
    private static void ConfigureDbContexts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("SqlPrimary")), ServiceLifetime.Scoped);
    }
    private static void ConfigureCustomServices(this IServiceCollection services)
    {
        services.AddScoped<IImmeubleRepository, ImmeubleRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddSingleton<IEmailService, EmailService>();
        services.AddScoped<ILikedProjectRepository, LikedProjectRepository>();
        services.AddScoped<IImmeubleFeatureRepository, ImmeubleFeatureRepository>();
        services.AddScoped<IProjectFeatureRepository, ProjectFeatureRepository>();
        services.AddScoped<ILeadRepository, LeadRepository>();
        services.AddScoped<IPerformanceIndicatorRepository, PerformanceIndicatorRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IImmeubleTrackingRepository, ImmeubleTrackingRepository>();
        services.AddScoped<INotaryAppointmentRepository, NotaryAppointmentRepository>();
        services.AddScoped<IEspaceTempsReelRepository, EspaceTempsReelRepository>();
        services.AddScoped<IProjectTypeBienRepository, ProjectTypeBienRepository>();
        services.AddScoped<IQuartierRepository, QuartierRepository>();
        services.AddScoped<ITypeBienRepository, TypeBienRepository>();
        services.AddScoped<IImmeubleTypeBienRepository, ImmeubleTypeBienRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IProjectAssignmentRepository, ProjectAssignmentRepository>();
        services.AddScoped<IProjectMembershipRepository, ProjectMembershipRepository>();
        services.AddScoped<IProjectAgentAssignmentConfigRepository, ProjectAgentAssignmentConfigRepository>();
        services.AddScoped<IAppointmentAssignmentHistoryRepository, AppointmentAssignmentHistoryRepository>();
        services.AddScoped<INotaryAppointmentAssignmentHistoryRepository, NotaryAppointmentAssignmentHistoryRepository>();
        services.AddScoped<IAppointmentVisitReportRepository, AppointmentVisitReportRepository>();
        services.AddScoped<IWeeklyAvailabilityRepository, WeeklyAvailabililtyRepository>();
        services.AddScoped<INotaryBlockRepository, NotaryBlockRepository>();
        services.AddScoped<IAgentWeeklyAvailabilityRepository, AgentWeeklyAvailabilityRepository>();
        services.AddScoped<IAgentBlockRepository, AgentBlockRepository>();
        services.AddScoped<IAgentDateOverrideRepository, AgentDateOverrideRepository>();
        services.AddScoped<IAgentAppointmentSettingsRepository, AgentAppointmentSettingsRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IPaymentTrackingRepository,PaymentTrackingRepository>();
        services.AddScoped<IPropertyDeliveryRepository, PropertyDeliveryRepository>();
        services.AddScoped<IAfterSaleClaimRepository, AfterSaleClaimRepository>();
        services.AddScoped<IClaimAttachmentRepository, ClaimAttachmentRepository>();
        services.AddScoped<IClaimCommentRepository,ClaimCommentRepository>();
        services.AddScoped<IClaimHistoryRepository, ClaimHistoryRepository>();


        services.AddScoped<DevelopmentDataSeeder>();

        // BlobServiceClient/BlobStorageSettings are registered in Program.cs
        // (same place as InternalApiSettings) since both are read directly
        // off IConfiguration at startup, not through this method's
        // `configuration` parameter's DI container state. BlobStorageService
        // itself is container-agnostic (see IBlobStorageService.ForContainer),
        // so Scoped vs Singleton no longer matters for cache correctness —
        // Scoped kept for consistency with the rest of this file.
        services.AddScoped<IBlobStorageService, BlobStorageService>();
    }

    #endregion
}
