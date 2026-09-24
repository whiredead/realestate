using ProjectAPI.Api.Application.Common.Behaviours;
using ProjectAPI.Api.Application.Common.Security;

namespace ProjectAPI.Api.Application;

/// <summary>
/// A static class containing extension methods to configure dependency injection for application services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    /// <summary>
    /// Adds application services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to which application services are added.</param>
    /// <returns>The modified <see cref="IServiceCollection"/> with added application services.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Adds Mapster services using the current executing assembly
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(Assembly.GetExecutingAssembly());
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        // Adds validators from the current executing assembly to the service collection
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Adds MediatR services to the service collection
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            // First: the acting user is the token's, before validation or the handler reads it.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Common.Behaviours.ActorStampingBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PaginationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            // §7 — applies only to commands implementing IIdempotentRequest;
            // runs after validation so a malformed request never consumes a key.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Common.Idempotency.IdempotencyBehaviour<,>));
        });

        // Keeps the legacy Purchase totals in step with the payment ledger (§14.3).
        services.AddScoped<Payments.PurchaseTotalsService>();

        // §3 / §7 — the single writer for a unit's commercial status. Scoped so it
        // shares the caller's DbContext and therefore the caller's transaction.
        services.AddScoped<Common.Units.IUnitStatusService, Common.Units.UnitStatusService>();
        // §1.1 — resolves the person behind a form submission to one CrmContact.
        services.AddScoped<Common.Crm.IContactResolver, Common.Crm.ContactResolver>();
        // Sales-agent auto-assignment: existing owner, else the project's
        // configured rule (round-robin/lowest-workload/primary-agent).
        services.AddScoped<Common.Assignment.ISalesAgentAssignmentService, Common.Assignment.SalesAgentAssignmentService>();
        // §1.1/§6.2 — invitation to activate an account for an approved,
        // account-less buyer; never a silent password, never a second contact.
        services.AddScoped<Common.Crm.IAccountInvitationService, Common.Crm.AccountInvitationService>();

        // §6.4 — caller identity and project perimeter. Scoped, not singleton:
        // both read the current request's claims, so they must not outlive it.
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<PermissionCacheVersion>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ProjectScopeService>();

        // §17.6 — single source of truth for notary eligibility, shared between
        // appointment creation and confirmation.
        services.AddScoped<Common.Notary.NotaryEligibilityService>();

        // §6.2/§7 — shared writer for per-user transactional notifications.
        services.AddScoped<Common.Notifications.INotificationService, Common.Notifications.NotificationService>();
        services.AddScoped<Common.Notifications.INotificationRecipientResolver, Common.Notifications.NotificationRecipientResolver>();
        services.AddScoped<Common.Notifications.INotificationFilter, Common.Notifications.AllowAllNotificationFilter>();
        services.AddScoped<Common.Notifications.INotificationRealtimePublisher, Common.Notifications.SignalRNotificationPublisher>();

        // §7.2 — every client-supplied media link (project images, site videos,
        // 3D tours) passes through here before it is stored.
        services.AddScoped<Common.Media.MediaUrlPolicy>();

        // §12.1 — the per-project document requirements a reservation must
        // satisfy before it may be submitted. Shared by the submit path and the
        // checklist the console renders, so the two can never disagree.
        services.AddScoped<Common.Reservations.ReservationDocumentChecklist>();

        return services;
    }
}
