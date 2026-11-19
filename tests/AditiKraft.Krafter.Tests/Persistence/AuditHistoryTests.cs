using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Tests.Persistence;

public sealed class AuditHistoryTests
{
    [Theory]
    [InlineData(SaveMode.Sync)]
    [InlineData(SaveMode.SyncAccept)]
    [InlineData(SaveMode.SyncKeepChanges)]
    [InlineData(SaveMode.Async)]
    [InlineData(SaveMode.AsyncAccept)]
    [InlineData(SaveMode.AsyncKeepChanges)]
    public async Task EverySaveOverloadStampsCreationAndTenantWithoutAcceptingChangesPrematurely(SaveMode mode)
    {
        DbContextOptions<ApplicationDbContext> options = Options();
        await using var insertion = Context(options, "tenant-a", "creator");
        var user = new ApplicationUser { Id = "subject", TenantId = "supplied-tenant", FirstName = "Before" };
        insertion.Users.Add(user);
        DateTime before = DateTime.UtcNow;

        Assert.Equal(1, await SaveAsync(insertion, mode));

        Assert.Equal(KeepsChanges(mode) ? EntityState.Added : EntityState.Unchanged, insertion.Entry(user).State);
        await using var verification = Context(options, "tenant-a", "reader");
        ApplicationUser saved = await verification.Users.SingleAsync();
        Assert.InRange(saved.CreatedOn, before, DateTime.UtcNow);
        Assert.Equal("creator", saved.CreatedById);
        Assert.Null(saved.UpdatedOn);
        Assert.Null(saved.UpdatedById);
        Assert.Equal("tenant-a", saved.TenantId);
    }

    [Theory]
    [InlineData(SaveMode.Sync, false)]
    [InlineData(SaveMode.Sync, true)]
    [InlineData(SaveMode.SyncAccept, false)]
    [InlineData(SaveMode.SyncAccept, true)]
    [InlineData(SaveMode.SyncKeepChanges, false)]
    [InlineData(SaveMode.SyncKeepChanges, true)]
    [InlineData(SaveMode.Async, false)]
    [InlineData(SaveMode.Async, true)]
    [InlineData(SaveMode.AsyncAccept, false)]
    [InlineData(SaveMode.AsyncAccept, true)]
    [InlineData(SaveMode.AsyncKeepChanges, false)]
    [InlineData(SaveMode.AsyncKeepChanges, true)]
    public async Task UpdatesAndSoftDeletesPreserveCreationAndStampTheCurrentActor(SaveMode mode, bool softDelete)
    {
        DbContextOptions<ApplicationDbContext> options = Options();
        DateTime createdOn = await InsertAsync(options);
        DateTime before;
        await using (var mutation = Context(options, "tenant-a", "updater"))
        {
            ApplicationUser user = await mutation.Users.SingleAsync();
            user.CreatedOn = createdOn.AddYears(-1);
            user.CreatedById = "supplied-creator";
            user.TenantId = "supplied-tenant";
            user.FirstName = "After";
            if (softDelete) mutation.Users.Remove(user);
            else mutation.Users.Update(user); // Identity also marks the full row as modified.
            before = DateTime.UtcNow;

            Assert.Equal(1, await SaveAsync(mutation, mode));

            Assert.Equal(KeepsChanges(mode) ? EntityState.Modified : EntityState.Unchanged,
                mutation.Entry(user).State);
            if (KeepsChanges(mode))
            {
                mutation.ChangeTracker.AcceptAllChanges();
                Assert.Equal(EntityState.Unchanged, mutation.Entry(user).State);
            }
        }

        await using (var verification = Context(options, "tenant-a", "reader"))
        {
            ApplicationUser saved = await verification.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(createdOn, saved.CreatedOn);
            Assert.Equal("creator", saved.CreatedById);
            Assert.NotNull(saved.UpdatedOn);
            Assert.InRange(saved.UpdatedOn.Value, before, DateTime.UtcNow);
            Assert.Equal("updater", saved.UpdatedById);
            Assert.Equal("tenant-a", saved.TenantId);
            Assert.Equal("After", saved.FirstName);
            Assert.Equal(softDelete, saved.IsDeleted);
            Assert.Equal(softDelete ? 0 : 1, await verification.Users.CountAsync());
        }

        await using var otherTenant = Context(options, "tenant-b", "reader");
        Assert.Empty(await otherTenant.Users.ToListAsync());
    }

    [Fact]
    public async Task DetachedFullRowUpdatesCannotReplacePersistedCreationHistory()
    {
        DbContextOptions<ApplicationDbContext> options = Options();
        DateTime createdOn = await InsertAsync(options);
        await using (var mutation = Context(options, "tenant-a", "updater"))
        {
            ApplicationUser user = await mutation.Users.AsNoTracking().SingleAsync();
            user.CreatedOn = createdOn.AddYears(-1);
            user.CreatedById = "supplied-creator";
            user.FirstName = "Detached update";
            mutation.Users.Update(user);
            await mutation.SaveChangesAsync();
        }

        await using var verification = Context(options, "tenant-a", "reader");
        ApplicationUser saved = await verification.Users.SingleAsync();
        Assert.Equal(createdOn, saved.CreatedOn);
        Assert.Equal("creator", saved.CreatedById);
        Assert.Equal("updater", saved.UpdatedById);
        Assert.Equal("Detached update", saved.FirstName);
    }

    [Fact]
    public async Task UpdateWithoutAnActorClearsThePreviousUpdaterAndPreservesCreation()
    {
        DbContextOptions<ApplicationDbContext> options = Options();
        DateTime createdOn = await InsertAsync(options);
        await using (var first = Context(options, "tenant-a", "updater"))
        {
            (await first.Users.SingleAsync()).FirstName = "First update";
            await first.SaveChangesAsync();
        }

        DateTime before = DateTime.UtcNow;
        await using (var background = Context(options, "tenant-a"))
        {
            ApplicationUser user = await background.Users.SingleAsync();
            Assert.Equal("updater", user.UpdatedById);
            user.FirstName = "Background update";
            await background.SaveChangesAsync();
        }

        await using var verification = Context(options, "tenant-a", "reader");
        ApplicationUser saved = await verification.Users.SingleAsync();
        Assert.Equal(createdOn, saved.CreatedOn);
        Assert.Equal("creator", saved.CreatedById);
        Assert.Null(saved.UpdatedById);
        Assert.NotNull(saved.UpdatedOn);
        Assert.InRange(saved.UpdatedOn.Value, before, DateTime.UtcNow);
    }

    private static async Task<DateTime> InsertAsync(DbContextOptions<ApplicationDbContext> options)
    {
        await using var insertion = Context(options, "tenant-a", "creator");
        var user = new ApplicationUser { Id = "subject", TenantId = "supplied-tenant", FirstName = "Before" };
        insertion.Users.Add(user);
        await insertion.SaveChangesAsync();
        return user.CreatedOn;
    }

    // InMemory verifies persisted audit and query-filter behavior, not relational constraints.
    private static DbContextOptions<ApplicationDbContext> Options() =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static ApplicationDbContext Context(DbContextOptions<ApplicationDbContext> options,
        string tenantId, string? actorId = null)
    {
        var actor = new CurrentUser();
        if (actorId is not null) actor.SetCurrentUserId(actorId);
        var tenant = new CurrentTenantService();
        tenant.SetTenant(new CurrentTenantDetails
        {
            Id = tenantId, Identifier = tenantId, Name = tenantId,
            TenantLink = $"https://{tenantId}.example.test", IsActive = true, ValidUpto = DateTime.MaxValue
        });
        return new ApplicationDbContext(options, actor, tenant);
    }

    private static Task<int> SaveAsync(ApplicationDbContext db, SaveMode mode) => mode switch
    {
        SaveMode.Sync => Task.FromResult(db.SaveChanges()),
        SaveMode.SyncAccept => Task.FromResult(db.SaveChanges(acceptAllChangesOnSuccess: true)),
        SaveMode.SyncKeepChanges => Task.FromResult(db.SaveChanges(acceptAllChangesOnSuccess: false)),
        SaveMode.Async => db.SaveChangesAsync(),
        SaveMode.AsyncAccept => db.SaveChangesAsync(acceptAllChangesOnSuccess: true),
        SaveMode.AsyncKeepChanges => db.SaveChangesAsync(acceptAllChangesOnSuccess: false),
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static bool KeepsChanges(SaveMode mode) => mode is SaveMode.SyncKeepChanges or SaveMode.AsyncKeepChanges;

    public enum SaveMode { Sync, SyncAccept, SyncKeepChanges, Async, AsyncAccept, AsyncKeepChanges }
}
