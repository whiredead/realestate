using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// Configure Logging
builder.Logging
    .ClearProviders()
    .AddConfiguration(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Logging
        .AddConsole()
        .AddDebug();
}

// Configure Services
builder.Services
  .AddHealthChecks()
  .AddCheck("Default", () => HealthCheckResult.Healthy("OKe"))
  // [You can add more checks here...]
  ;

builder.Services
    // Registers the Swagger generator, defining one Swagger document.
    .AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JWTToken_Auth_API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer Auth Token\"",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
})    
    // Registers services to compress outputs.
    .AddResponseCompression()
    // Registers sensitive encryption services (e.g. to encrypt cookies).
    .AddDataProtection();


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, ProjectAPI.Api.Hubs.NotificationUserIdProvider>();

// There is no service-to-service API-key scheme any more: authentication and
// project management run in one process, so what used to be an authenticated
// HTTP hop between them is now an in-process MediatR send and the shared key
// it required no longer exists.

// The whole storage account (and default container) lives in these two
// appsettings values — change either one and every upload path (images,
// reservation documents, anything future) follows without touching code.
var blobStorageSettings = new ProjectAPI.Infrastructure.Settings.BlobStorageSettings
{
    ConnectionString = builder.Configuration["BlobStorage:ConnectionString"] ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not configured."),
    ContainerName = builder.Configuration["BlobStorage:ContainerName"] ?? throw new InvalidOperationException("BlobStorage:ContainerName is not configured.")
};
builder.Services.AddSingleton(blobStorageSettings);
builder.Services.AddSingleton(new Azure.Storage.Blobs.BlobServiceClient(blobStorageSettings.ConnectionString));

// §7.2 — host allow-lists for client-supplied media links. The storage account
// above is always accepted without appearing here, so rotating it never needs
// a matching edit in appsettings (see MediaUrlPolicy).
builder.Services.Configure<ProjectAPI.Api.Application.Common.Media.MediaSettings>(
    builder.Configuration.GetSection(ProjectAPI.Api.Application.Common.Media.MediaSettings.SectionName));

// Outbound mail. The password used to be compiled into EmailService, which put
// a live mailbox password in source control; it now comes from configuration
// (user-secrets locally, Smtp__* environment variables in a deployment).
// Nothing in ProjectAPI actually sends mail today — IEmailService has no
// callers here — so an unconfigured mailbox is not a startup error; the
// registration exists only so the service can still be resolved.
builder.Services.AddSingleton(new ProjectAPI.Infrastructure.Settings.SmtpSettings
{
    Host = builder.Configuration["Smtp:Host"] ?? "smtp.gmail.com",
    Port = int.TryParse(builder.Configuration["Smtp:Port"], out var smtpPort) ? smtpPort : 587,
    UserName = builder.Configuration["Smtp:UserName"] ?? string.Empty,
    Password = builder.Configuration["Smtp:Password"] ?? string.Empty,
    FromAddress = builder.Configuration["Smtp:FromAddress"],
    EnableSsl = !bool.TryParse(builder.Configuration["Smtp:EnableSsl"], out var ssl) || ssl,
});

// The invitation-acceptance flow no longer needs an HTTP client: it validates
// the AccountInvitation token and then sends ProvisionInternalUserCommand.
// The identity module is still the only thing that writes AspNetUsers (§6.1) —
// that is now a module boundary rather than a network boundary.

builder.Services
    // Registers MVC & Web API services.
    .AddMvcCore(
        options => { options.Filters.Add<ApiExceptionFilter>(); options.Filters.AddService<EndpointPermissionFilter>(); }
    )
    .AddApiExplorer()
    .AddDataAnnotations()
    .AddAuthorization(options =>
    {
        // §6.4 — "les contrôles d'accès sont appliqués côté API". Fail closed:
        // every endpoint requires an authenticated caller unless it explicitly
        // opts out with [AllowAnonymous] (public catalogue, visitor appointment
        // request, public feedback). Role- and perimeter-level checks are layered
        // on top of this in the controllers/handlers.
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

// [You can add your own application services here...]
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration).AddCors(options =>
    {
        options.AddPolicy("AllowAll", builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });

        // The SignalR client sends credentials on /hubs/notifications/negotiate, and
        // browsers reject a "*" Access-Control-Allow-Origin for credentialed
        // requests — so the hub gets its own policy that echoes back only known
        // origins: explicit entries of AllowedOrigins, plus localhost in Development.
        var hubOrigins = (builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
            .Where(o => o != "*")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isDevelopment = builder.Environment.IsDevelopment();
        options.AddPolicy("NotificationHub", builder =>
        {
            builder.SetIsOriginAllowed(origin =>
                       hubOrigins.Contains(origin) ||
                       (isDevelopment && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback))
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
    });
builder.Services.AddScoped<EndpointPermissionFilter>();
// Model-binding failures use the same RFC 9457 shape (French, code, requestId) as every other error.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ProjectAPI.Api.Filters.ModelStateProblemFactory.Create);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, ProjectAPI.Api.Hubs.NotificationUserIdProvider>();


// AddIdentity (in Infrastructure) registers cookie schemes and makes the cookie
// the default challenge, so an unauthenticated API call would 302-redirect to a
// login page instead of returning 401. Re-assert Bearer as the default so the
// API answers with the status codes the contract requires (§31.3: 401/403).
builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});

// Scheduled jobs (spec §7, §49.5)
builder.Services.AddHostedService<ProjectAPI.Api.BackgroundJobs.ReservationExpiryJob>();
builder.Services.AddHostedService<ProjectAPI.Api.BackgroundJobs.AppointmentReminderJob>();
builder.Services.AddHostedService<ProjectAPI.Api.BackgroundJobs.InstallmentOverdueJob>();
builder.Services.AddHostedService<ProjectAPI.Api.BackgroundJobs.SavSlaDetectionJob>();

var app = builder.Build();

// Seed reference and workflow data (spec §6.1, §14–§17).
//
// Development-only AND opt-in via SeedData, so a production start can never
// reach it even if the environment is misconfigured. The seeder itself is
// idempotent and insert-only, so repeated dev restarts are harmless.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedData"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider
        .GetRequiredService<ProjectAPI.Infrastructure.Seeding.DevelopmentDataSeeder>();
    await seeder.SeedAsync();
}

// Configure Application middlewares pipeline
if (builder.Environment.IsDevelopment())
{
    // Uses development tools.
    app
        .UseDeveloperExceptionPage();
}


app.UseSwagger();
app.UseSwaggerUI();
app
    .UseRouting()
    .UseStaticFiles()
    .UseCors("AllowAll") // Apply the CORS policy
    .UseResponseCompression()
    .UseAuthentication()
    .UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.MapHub<ProjectAPI.Api.Hubs.NotificationHub>("/hubs/notifications").RequireCors("NotificationHub");

app.Run();
