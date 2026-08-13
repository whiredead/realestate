using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Domain.ApplicationUser.Interfaces;
using AuthenticationAPI.Domain.Common.Interfaces;
using AuthenticationAPI.Infrastructure.Context;
using AuthenticationAPI.Infrastructure.Providers;
using AuthenticationAPI.Infrastructure.Security;
using AuthenticationAPI.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AuthenticationAPI.Infrastructure;

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
        services.ConfigureSettings(configuration);
        services.ConfigureDbContexts(configuration);
        services.ConfigureIdentity();
        services.ConfigureJwtAuthentication(configuration);
        services.ConfigureInternalApiKeyAuthentication(configuration);
        services.ConfigureIdentityOptions();
        services.ConfigureCustomServices(configuration);
        services.ConfigureProjectApiClient();

        return services;
    }

    #region Helpers

    private static void ConfigureSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = new JwtSettings();
        var otpSettings = new OtpSettings();
        var internalApiSettings = new InternalApiSettings();

        configuration.Bind("JwtSettings", jwtSettings);
        configuration.Bind("OtpSettings", otpSettings);
        configuration.Bind("InternalApi", internalApiSettings);

        services
            .AddSingleton(jwtSettings)
            .AddSingleton(otpSettings)
            .AddSingleton(internalApiSettings);
    }

    private static void ConfigureDbContexts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("SqlPrimary")), ServiceLifetime.Scoped);
    }

    private static void ConfigureIdentity(this IServiceCollection services)
    {
        services.AddIdentity<User, Role>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
    }

    private static void ConfigureJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = new JwtSettings();
        configuration.Bind("JwtSettings", jwtSettings);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
            };
        });
    }

    /// <summary>
    /// Adds InternalApiKeyDefaults.AuthenticationScheme alongside the default
    /// JwtBearer scheme registered in ConfigureJwtAuthentication above — using
    /// AddAuthentication() again here (with no options delegate) merges into
    /// the same builder rather than resetting DefaultAuthenticateScheme, so
    /// every existing [Authorize] (and the global FallbackPolicy) keeps
    /// requiring a JWT exactly as before. Only a controller/action that
    /// explicitly opts in with
    /// [Authorize(AuthenticationSchemes = InternalApiKeyDefaults.AuthenticationScheme)]
    /// is authenticated by this scheme instead.
    /// </summary>
    private static void ConfigureInternalApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication()
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InternalApiKeyAuthenticationHandler>(
                InternalApiKeyDefaults.AuthenticationScheme, _ => { });
    }

    private static void ConfigureIdentityOptions(this IServiceCollection services)
    {
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
        });
    }

    private static void ConfigureCustomServices(this IServiceCollection services, IConfiguration configuration)
    {
        // SMTP credentials come from configuration (user-secrets locally,
        // Smtp__* environment variables in a deployment). They used to be
        // compiled into EmailService, which put a live mailbox password in
        // source control. Only the password-reset flow sends mail today, so an
        // unconfigured mailbox is tolerated at startup and fails at send time
        // instead of preventing the API from booting.
        var smtpSettings = new SmtpSettings
        {
            Host = configuration["Smtp:Host"] ?? "smtp.gmail.com",
            Port = int.TryParse(configuration["Smtp:Port"], out var smtpPort) ? smtpPort : 587,
            UserName = configuration["Smtp:UserName"] ?? string.Empty,
            Password = configuration["Smtp:Password"] ?? string.Empty,
            FromAddress = configuration["Smtp:FromAddress"],
            EnableSsl = !bool.TryParse(configuration["Smtp:EnableSsl"], out var ssl) || ssl,
        };

        services
            .AddSingleton(smtpSettings)
            .AddSingleton<ITokenProvider, TokenProvider>()
            .AddSingleton<IEmailService, EmailService>()
            .AddSingleton<IOtpVerificationRepository, OtpVerificationRepository>()
            .AddScoped<IWeeklyAvailabilityRepository, WeeklyAvailabilityRepository>()
            .AddScoped<IPerformanceIndicatorRepository, PerformanceIndicatorRepository>()
            .AddScoped<IUserRepository, UserRepository>();
    }

    /// <summary>Mirrors a newly created internal account into ProjectAPI's own AspNetUsers table — see IProjectApiClient.</summary>
    private static void ConfigureProjectApiClient(this IServiceCollection services)
    {
        services.AddHttpClient<Clients.IProjectApiClient, Clients.ProjectApiClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<InternalApiSettings>();
            client.BaseAddress = new Uri(settings.ProjectApiBaseUrl);
        });
    }

    #endregion
}
