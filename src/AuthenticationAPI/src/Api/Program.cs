using Microsoft.AspNetCore.Authorization;

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
  .AddCheck("Default", () => HealthCheckResult.Healthy("OK"))
  // [You can add more checks here...]
  ;

builder.Services
    // Registers the Swagger generator, defining one Swagger document.
    .AddEndpointsApiExplorer()
    .AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    })
    // Registers services to compress outputs.
    .AddResponseCompression()
    // Registers sensitive encryption services (e.g. to encrypt cookies).
    .AddDataProtection();

// The Bearer scheme is configured in Infrastructure (ConfigureJwtAuthentication).
// It was registered but never applied — the pipeline was missing
// UseAuthentication (added below), so [Authorize] had no effect and every
// endpoint was effectively anonymous.

builder.Services
    // Registers MVC & Web API services.
    .AddMvcCore(
        options => options.Filters.Add<ApiExceptionFilter>()
    )
    .AddApiExplorer()
    .AddDataAnnotations()
    .AddAuthorization(options =>
    {
        // §6.4 — fail closed: every endpoint requires an authenticated caller
        // unless it opts out with [AllowAnonymous]. This closes the hole where
        // anonymous callers could read the whole user directory.
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

// Lets handlers (e.g. RegisterHandler's §6.2 internal-role guard) read the
// authenticated caller's claims.
builder.Services.AddHttpContextAccessor();

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


var app = builder.Build();

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
    .UseCors("AllowAll") // Apply the CORS policy
    .UseResponseCompression()
    .UseAuthentication()
    .UseAuthorization();

// Liveness probe stays open — it carries no data and is polled without a token.
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program;
