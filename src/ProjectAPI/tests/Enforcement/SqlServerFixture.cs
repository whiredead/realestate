using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// A real SQL Server database, built by running the actual migrations.
///
/// These tests exist to prove that database-level constraints fire — a filtered
/// unique index and two CHECK constraints. An in-memory or SQLite provider would
/// silently not enforce them, so the suite would pass while the production
/// database enforced nothing. That is precisely the failure mode this work order
/// was written to close, so the fixture uses the same provider as production and
/// applies the same migrations.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Defaults to the same instance the app itself targets (see
    // ProjectAPI/src/Api/appsettings.json, ConnectionStrings:SqlPrimary) —
    // LocalDB, which is what actually ships on a dev machine. Overridable via
    // GPIA_TEST_SQL_SERVER for anyone running a named SQL Server instance
    // instead.
    private static readonly string Server =
        Environment.GetEnvironmentVariable("GPIA_TEST_SQL_SERVER") ?? @"(localdb)\MSSQLLocalDB";
    public const string DatabaseName = "GPIA_Project_EnforcementTests";

    public string ConnectionString =>
        $"Server={Server};Database={DatabaseName};Trusted_Connection=True;" +
        "MultipleActiveResultSets=true;TrustServerCertificate=True";

    private string MasterConnectionString =>
        $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>True when SQL Server could not be reached; tests skip rather than fail.</summary>
    public bool Available { get; private set; }

    public string? UnavailableReason { get; private set; }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// A real UserManager&lt;User&gt; backed by this test database — needed by
    /// tests exercising handlers that validate a target user's actual
    /// AspNetUserRoles (e.g. CreateProjectMembershipHandler's "role must
    /// match the account's platform role" check). The returned
    /// IServiceScope owns the UserManager's lifetime; dispose it when done.
    /// </summary>
    public (IServiceScope Scope, UserManager<User> UserManager) CreateUserManager()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(ConnectionString));
        services.AddLogging();
        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<UserManager<User>>());
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Dedicated database — never the dev one. Dropped and rebuilt so each
            // run starts from the migrations, not from leftover state.
            await using (var master = new Microsoft.Data.SqlClient.SqlConnection(MasterConnectionString))
            {
                await master.OpenAsync();
                await using var drop = master.CreateCommand();
                drop.CommandText = $@"
                    IF DB_ID('{DatabaseName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{DatabaseName}];
                    END";
                await drop.ExecuteNonQueryAsync();
            }

            await using var db = CreateContext();
            await db.Database.MigrateAsync();

            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            UnavailableReason = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (!Available) return;

        try
        {
            await using var master = new Microsoft.Data.SqlClient.SqlConnection(MasterConnectionString);
            await master.OpenAsync();
            await using var drop = master.CreateCommand();
            drop.CommandText = $@"
                IF DB_ID('{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END";
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Leaving a test database behind must never fail the suite.
        }
    }
}

[CollectionDefinition("sqlserver")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
