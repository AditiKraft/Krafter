using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AditiKraft.Krafter.Tests.Persistence;

public sealed class GoogleOAuthMigrationTests
{
    [Fact]
    public void PostgreSqlOAuthModelAndMigrationMatchAndKeepStateSeparateFromTenantAccounts()
    {
        using var db = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql("Host=localhost;Database=model_check;Username=model_check;Password=unused").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var entity = db.Model.FindEntityType(typeof(GoogleLoginTransaction))!;
        Assert.Equal("GoogleLoginTransactions", entity.GetTableName());
        Assert.Equal(64, entity.FindProperty(nameof(GoogleLoginTransaction.Id))!.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Single().Name == nameof(GoogleLoginTransaction.ExpiresAt));
        string upgrade = db.GetService<IMigrator>().GenerateScript("20260912000000_UniqueTenantIdentifiers", "20261108180000_AddGoogleOAuthTransactions");
        Assert.Contains("CREATE TABLE \"GoogleLoginTransactions\"", upgrade);
        Assert.Contains("\"ProtectedPayload\" text NOT NULL", upgrade);
        Assert.Contains("\"IX_GoogleLoginTransactions_ExpiresAt\"", upgrade);
        string downgrade = db.GetService<IMigrator>().GenerateScript("20261108180000_AddGoogleOAuthTransactions", "20260912000000_UniqueTenantIdentifiers");
        Assert.Contains("DROP TABLE \"GoogleLoginTransactions\"", downgrade);
        Assert.DoesNotContain("DROP TABLE \"Tenant\"", downgrade);
    }
}
