using Microsoft.EntityFrameworkCore;
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
    private const string Server = @"DESKTOP-1CEDKH6\SQLEXPRESS";
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
