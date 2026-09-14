using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]))
        };
    })
    // Service-to-service calls from AuthenticationAPI (e.g. mirroring a
    // newly admin-created account into this service's own AspNetUsers
    // table) — a separate scheme, only opted into by InternalController.
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ProjectAPI.Infrastructure.Security.InternalApiKeyAuthenticationHandler>(
        ProjectAPI.Infrastructure.Security.InternalApiKeyDefaults.AuthenticationScheme, _ => { });

builder.Services.AddSingleton(new ProjectAPI.Infrastructure.Settings.InternalApiSettings
{
    ApiKey = builder.Configuration["InternalApi:ApiKey"] ?? throw new InvalidOperationException("InternalApi:ApiKey is not configured."),
    AuthApiBaseUrl = builder.Configuration["InternalApi:AuthApiBaseUrl"] ?? throw new InvalidOperationException("InternalApi:AuthApiBaseUrl is not configured.")
});

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

// Phase 2 invitation-acceptance flow: this service validates the
// AccountInvitation token, then calls AuthenticationAPI's own internal
// endpoint to actually create the account (AuthenticationAPI is the source
// of truth for accounts; ProjectAPI never writes AspNetUsers directly).
builder.Services.AddHttpClient<ProjectAPI.Infrastructure.Clients.IAuthenticationApiClient, ProjectAPI.Infrastructure.Clients.AuthenticationApiClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<ProjectAPI.Infrastructure.Settings.InternalApiSettings>();
    client.BaseAddress = new Uri(settings.AuthApiBaseUrl);
});

builder.Services
    // Registers MVC & Web API services.
    .AddMvcCore(
        options => options.Filters.Add<ApiExceptionFilter>()
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
    });


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

app.Run();
