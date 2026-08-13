using Microsoft.AspNetCore.Authorization;
using System.Linq;

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

// TEMPORARY, dev-only — lets us set a known password on a seeded account so
// the workflow can be tested end-to-end before the invitation flow exists.
// Remove once testing is done; never ship this.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/set-password", async (
        DevSetPasswordRequest request,
        Microsoft.AspNetCore.Identity.UserManager<AuthenticationAPI.Domain.ApplicationUser.Entities.User> userManager) =>
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null) return Results.NotFound($"No user with email {request.Email}");

        var removeResult = await userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded && removeResult.Errors.Any(e => e.Code != "PasswordMismatch"))
        {
            // RemovePasswordAsync fails harmlessly if there's no password set yet.
        }

        var addResult = await userManager.AddPasswordAsync(user, request.NewPassword);
        if (!addResult.Succeeded)
        {
            return Results.BadRequest(addResult.Errors.Select(e => e.Description));
        }

        return Results.Ok(new { user.Email, Message = "Password set." });
    }).AllowAnonymous();
}

app.MapControllers();

app.Run();

public partial class Program;

/// <summary>TEMPORARY — see the /dev/set-password endpoint above. Remove together.</summary>
public record DevSetPasswordRequest(string Email, string NewPassword);
