using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace AditiKraft.Krafter.Tests.Users;

internal static class DomainTestServices
{
    public static AppUrls Urls { get; } = new()
    { RootUiUrl = "https://app.example.test", TenantBaseDomain = "example.test", ApiBaseUrl = "https://api.example.test" };

    public static void Register(IServiceCollection services, string tenantId, string? actorId = null,
        string? postgres = null, IInterceptor? identityInterceptor = null, IInterceptor? registryInterceptor = null)
    {
        string database = Guid.NewGuid().ToString();
        services.AddLogging();
        services.TryAddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.TryAddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = Environments.Production });
        services.AddScoped<CurrentTenantService>(_ =>
        {
            var tenant = new CurrentTenantService();
            tenant.SetTenant(Tenant(tenantId));
            return tenant;
        });
        services.AddScoped<ITenantGetterService>(p => p.GetRequiredService<CurrentTenantService>());
        services.AddScoped<ITenantSetterService>(p => p.GetRequiredService<CurrentTenantService>());
        services.AddScoped<ICurrentUser>(_ =>
        {
            var user = new CurrentUser();
            if (actorId is not null) user.SetCurrentUser(Principal(actorId));
            return user;
        });
        services.AddScoped<ICurrentUserInitializer>(p => (ICurrentUserInitializer)p.GetRequiredService<ICurrentUser>());
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (postgres is null) options.UseInMemoryDatabase(database + "-identity");
            else options.UseNpgsql(postgres);
            if (identityInterceptor is not null) options.AddInterceptors(identityInterceptor);
        });
        services.AddDbContext<TenantDbContext>(options =>
        {
            if (postgres is null) options.UseInMemoryDatabase(database + "-registry");
            else options.UseNpgsql(postgres);
            if (registryInterceptor is not null) options.AddInterceptors(registryInterceptor);
        });
        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserMutationService, UserMutationService>();
        services.AddScoped<RolePermissionService>();
        services.AddScoped<DataSeedService>();
        services.AddSingleton<DomainJobs>();
        services.AddSingleton<IJobService>(p => p.GetRequiredService<DomainJobs>());
    }

    public static ServiceProvider Provider(string tenantId, string? actorId = null, string? postgres = null,
        IInterceptor? identityInterceptor = null, IInterceptor? registryInterceptor = null)
    {
        var services = new ServiceCollection();
        Register(services, tenantId, actorId, postgres, identityInterceptor, registryInterceptor);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static CurrentTenantDetails Tenant(string id) => new()
    { Id = id, Identifier = "blue", Name = "Blue", TenantLink = "https://blue.example.test", IsActive = true, ValidUpto = DateTime.MaxValue };

    public static void SelectTenant(IServiceProvider services, string id)
        => services.GetRequiredService<ITenantSetterService>().SetTenant(Tenant(id));

    // Deliberately stale claims cannot substitute for the production live permission query.
    public static ClaimsPrincipal Principal(string id) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id), new Claim(AppClaimTypes.Permission, "Permissions.RoleClaims.Update"),
         new Claim(AppClaimTypes.Permission, "Permissions.RoleClaims.View")], "domain-tests"));

    public static async Task<ApplicationUser> AddUserAsync(IServiceProvider services, string email, string? id = null,
        bool owner = false)
    {
        var user = new ApplicationUser
        { Id = id ?? Guid.NewGuid().ToString(), FirstName = "First", LastName = "Last", Email = email, UserName = email, IsActive = true, IsOwner = owner };
        IdentityResult created = await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "TestPassword1!");
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
        return user;
    }

    public static async Task<ApplicationRole> AddRoleAsync(IServiceProvider services, string name)
    {
        var role = new ApplicationRole(name) { Id = Guid.NewGuid().ToString() };
        Assert.True((await services.GetRequiredService<RoleManager<ApplicationRole>>().CreateAsync(role)).Succeeded);
        return role;
    }

    public static async Task MigrateAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
    }
}

internal sealed class DomainJobs : IJobService
{
    public List<SendEmailRequestInput> Emails { get; } = [];
    public Func<Task>? BeforeEnqueue { get; set; }

    public async Task EnqueueAsync<T>(T input, string methodName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BeforeEnqueue is not null) await BeforeEnqueue();
        Emails.Add(Assert.IsType<SendEmailRequestInput>(input));
    }
}
