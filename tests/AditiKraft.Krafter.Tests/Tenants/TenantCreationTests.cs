using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class TenantCreationTests
{
    [Fact]
    public async Task CreatingTenantPersistsUtcCreationTimeAndUpdatingPreservesIt()
    {
        var services = new ServiceCollection();
        string databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddTenantServices();
        services.AddDbContext<TenantDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase($"{databaseName}-identity"));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<DataSeedService>();
        services.AddSingleton<IJobService, NoExternalJobs>();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IServiceProvider scoped = scope.ServiceProvider;
        scoped.GetRequiredService<ITenantSetterService>().SetTenant(new CurrentTenantDetails
        {
            Id = "root",
            TenantLink = "https://root.example.com"
        });
        var currentUser = (CurrentUser)scoped.GetRequiredService<ICurrentUser>();
        currentUser.SetCurrentUserId("creator");
        TenantDbContext db = scoped.GetRequiredService<TenantDbContext>();
        ITenantGetterService tenant = scoped.GetRequiredService<ITenantGetterService>();
        var request = new CreateOrUpdateTenantRequest
        {
            Name = "Example tenant",
            Identifier = "example",
            AdminEmail = "admin@example.com",
            IsActive = true,
            ValidUpto = DateTime.UtcNow.AddDays(30)
        };
        var create = new CreateTenant.Handler(db, tenant, provider, currentUser);

        DateTime before = DateTime.UtcNow;
        Response created = await create.CreateAsync(request, CancellationToken.None);
        DateTime after = DateTime.UtcNow;

        Assert.False(created.IsError, created.Message);
        db.ChangeTracker.Clear();
        Tenant saved = await db.Tenants.AsNoTracking().SingleAsync();
        Assert.InRange(saved.CreatedOn, before, after);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedOn.Kind);
        Assert.Equal("creator", saved.CreatedById);

        request.Name = "Updated tenant";
        var update = new UpdateTenant.Handler(db, tenant, provider);
        Response updated = await update.UpdateAsync(saved.Id!, request, CancellationToken.None);

        Assert.False(updated.IsError, updated.Message);
        db.ChangeTracker.Clear();
        Tenant reloaded = await db.Tenants.AsNoTracking().SingleAsync();
        Assert.Equal("Updated tenant", reloaded.Name);
        Assert.Equal(saved.CreatedOn, reloaded.CreatedOn);
    }

    private sealed class NoExternalJobs : IJobService
    {
        public Task EnqueueAsync<T>(T requestInput, string methodName, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
