using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AditiKraft.Krafter.Tests.Persistence;

public sealed class PersistenceTests
{
    [Fact]
    public void PostgreSqlTenantMigrationEnforcesCaseInsensitiveUniquenessForNondeletedTenants()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql("Host=localhost;Database=model_check;Username=model_check;Password=unused")
            .Options;
        using var db = new TenantDbContext(options);

        Assert.False(db.Database.HasPendingModelChanges());
        IMigrator migrator = db.GetService<IMigrator>();
        string upgrade = migrator.GenerateScript("20260428150352_FirstMigTenant", "20260912000000_UniqueTenantIdentifiers");
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Tenant_Identifier_Lower\"", upgrade);
        Assert.Contains("ON \"Tenant\" (lower(\"Identifier\"))", upgrade);
        Assert.Contains("WHERE NOT \"IsDeleted\"", upgrade);

        string downgrade = migrator.GenerateScript("20260912000000_UniqueTenantIdentifiers", "20260428150352_FirstMigTenant");
        Assert.Contains("DROP INDEX \"IX_Tenant_Identifier_Lower\"", downgrade);
        Assert.DoesNotContain("DROP TABLE", downgrade);
    }

    [Fact]
    public void PostgreSqlModelMatchesExistingMigrations()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_check;Username=model_check;Password=unused")
            .Options;
        using var db = new ApplicationDbContext(options, new CurrentUser(), new CurrentTenantService());

        // Model comparison does not connect to a database.
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task HistoryRequestReturnsBadRequestWithoutQueryingDatabase()
    {
        await using var db = new TenantDbContext(new DbContextOptions<TenantDbContext>());

        var result = await new GetTenants.Handler(db).GetAsync(new GetRequestInput { History = true },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("history is not supported", result.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TenantListKeepsCurrentAndDeletedRecordsSeparate(bool deleted)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new TenantDbContext(options);
        db.Tenants.AddRange(
            new Tenant { Id = "current", Name = "Current", Identifier = "current", AdminEmail = "a@example.com" },
            new Tenant { Id = "deleted", Name = "Deleted", Identifier = "deleted", AdminEmail = "b@example.com", IsDeleted = true });
        await db.SaveChangesAsync();

        var result = await new GetTenants.Handler(db).GetAsync(
            new GetRequestInput { IsDeleted = deleted, MaxResultCount = 10 }, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(deleted ? "deleted" : "current", Assert.Single(result.Data!.Items).Id);
        Assert.Equal(1, result.Data.TotalCount);
    }
}
