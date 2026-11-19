using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace AditiKraft.Krafter.Tests.Persistence;

// Each test migrates only its own random schema in the dedicated KRAFTER_TEST_POSTGRES database.
[Collection("MFA PostgreSQL")]
public sealed class RoleNameIndexPostgresTests
{
    private const string FirstMigration = "20260428165759_FirstMig";
    private const string RemoveIndexMigration = "20260428165838_RemoveRoleNameIndex";

    [PostgresMfaFact]
    public async Task HistoricalRoleIndexCanRollBackAndUpgradeAgainOnPostgreSql()
    {
        await using IsolatedSchema schema = await IsolatedSchema.CreateAsync();
        await using ApplicationDbContext db = schema.Context();
        IMigrator migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(RemoveIndexMigration);
        await AddRoleAsync(db, "role-a", "tenant-a");

        await migrator.MigrateAsync(FirstMigration);

        Assert.True(await HasRoleIndexAsync(schema));
        Assert.Equal([FirstMigration], (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(1L, await CountRolesAsync(schema));

        await migrator.MigrateAsync(RemoveIndexMigration);

        Assert.False(await HasRoleIndexAsync(schema));
        Assert.Equal([FirstMigration, RemoveIndexMigration], (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(1L, await CountRolesAsync(schema));
    }

    [PostgresMfaFact]
    public async Task RollbackRefusesDuplicateNamesAcrossTenantsWithoutLosingDataOrMigrationHistory()
    {
        await using IsolatedSchema schema = await IsolatedSchema.CreateAsync();
        await using ApplicationDbContext db = schema.Context();
        IMigrator migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(RemoveIndexMigration);
        await AddRoleAsync(db, "role-a", "tenant-a");
        await AddRoleAsync(db, "role-b", "tenant-b");

        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(FirstMigration));

        // The old schema had global uniqueness: duplicate names must be reconciled before downgrade.
        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.False(await HasRoleIndexAsync(schema));
        Assert.Equal([FirstMigration, RemoveIndexMigration], (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(2L, await CountRolesAsync(schema));
    }

    private static Task<int> AddRoleAsync(ApplicationDbContext db, string id, string tenantId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ApplicationRole" ("Id", "Name", "NormalizedName", "TenantId", "IsDeleted")
            VALUES ({id}, 'Admin', 'ADMIN', {tenantId}, FALSE)
            """);

    private static async Task<bool> HasRoleIndexAsync(IsolatedSchema schema)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT FROM pg_indexes WHERE schemaname = @schema AND indexname = 'RoleNameIndex')
            """, schema.Connection);
        command.Parameters.AddWithValue("schema", schema.Name);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> CountRolesAsync(IsolatedSchema schema)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM \"ApplicationRole\"", schema.Connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class IsolatedSchema(NpgsqlConnection connection, string name) : IAsyncDisposable
    {
        public NpgsqlConnection Connection => connection;
        public string Name => name;

        public static async Task<IsolatedSchema> CreateAsync()
        {
            string name = "krafter_role_rollback_" + Guid.NewGuid().ToString("N");
            var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!)
            {
                SearchPath = name,
                Pooling = false
            };
            var connection = new NpgsqlConnection(settings.ConnectionString);
            try
            {
                await connection.OpenAsync();
                // The identifier is generated here, never supplied by a caller or configuration.
                await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{name}\"", connection);
                await command.ExecuteNonQueryAsync();
                return new IsolatedSchema(connection, name);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public ApplicationDbContext Context()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", name))
                .Options;
            return new ApplicationDbContext(options, new CurrentUser(), new CurrentTenantService());
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand($"DROP SCHEMA \"{name}\" CASCADE", connection);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
