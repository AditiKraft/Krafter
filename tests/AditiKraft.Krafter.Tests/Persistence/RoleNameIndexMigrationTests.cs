using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AditiKraft.Krafter.Tests.Persistence;

public sealed class RoleNameIndexMigrationTests
{
    [Fact]
    public void HistoricalRoleIndexRollbackUsesPostgreSqlQuoting()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_check;Username=model_check;Password=unused")
            .Options;
        using var db = new ApplicationDbContext(options, new CurrentUser(), new CurrentTenantService());

        // Generating this script does not connect to PostgreSQL or modify any database.
        string downgrade = db.GetService<IMigrator>().GenerateScript(
            "20260428165838_RemoveRoleNameIndex", "20260428165759_FirstMig");

        Assert.Contains("CREATE UNIQUE INDEX \"RoleNameIndex\" ON \"ApplicationRole\" (\"NormalizedName\") " +
                        "WHERE \"NormalizedName\" IS NOT NULL;", downgrade);
        Assert.DoesNotContain("[NormalizedName]", downgrade);
    }
}
