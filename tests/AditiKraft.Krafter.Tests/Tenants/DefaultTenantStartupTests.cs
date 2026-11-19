using System.Net;
using System.Net.Http.Json;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class DefaultTenantStartupTests
{
    private const string BootstrapPassword = "BootstrapStartupTestPassword1!";

    [Fact]
    public async Task SharedBackendStartupAwaitsRootProvisioningInAnIndependentTenantScope()
    {
        var pause = new PauseFirstSave();
        await using WebApplication app = BuildApp(pause);
        await AddRegistryAsync(app);
        await using AsyncServiceScope unrelated = app.Services.CreateAsyncScope();
        SetTenant(unrelated.ServiceProvider, "unrelated");
        ApplicationDbContext unrelatedDb = unrelated.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        Task starting = app.StartAsync(deadline.Token);
        try
        {
            await pause.Entered.Task.WaitAsync(deadline.Token);
            Assert.False(starting.IsCompleted);
            Assert.False(app.Lifetime.ApplicationStarted.IsCancellationRequested);
            pause.Resume.TrySetResult();
            await starting.WaitAsync(deadline.Token);

            await using AsyncServiceScope root = app.Services.CreateAsyncScope();
            SetTenant(root.ServiceProvider, SeedDataConstants.RootTenant.Id);
            ApplicationUser owner = await root.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync();
            Assert.Equal(SeedDataConstants.RootUser.Id, owner.Id);
            Assert.True(await root.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .CheckPasswordAsync(owner, BootstrapPassword));
            Assert.Empty(await unrelatedDb.Users.ToListAsync());
            Assert.Equal("unrelated", unrelated.ServiceProvider.GetRequiredService<ITenantGetterService>().Tenant.Id);
            Assert.Empty(app.Services.GetRequiredService<IdentityTestContext.RecordingJobService>().Emails);
        }
        finally { pause.Resume.TrySetResult(); }
    }

    [Fact]
    public async Task ProvisioningFailurePreventsApplicationStartup()
    {
        await using WebApplication app = BuildApp(rejectDefaultPassword: true);
        await AddRegistryAsync(app);

        await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());

        Assert.False(app.Lifetime.ApplicationStarted.IsCancellationRequested);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("blue")]
    public async Task AnonymousRequestsCannotReseedRootOrHeaderSelectedTenant(string tenantId)
    {
        await using WebApplication app = BuildApp();
        await AddRegistryAsync(app);
        await app.StartAsync();
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        SetTenant(scope.ServiceProvider, tenantId);
        await scope.ServiceProvider.GetRequiredService<DataSeedService>().SeedAsync();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationRoleClaim revoked = await db.RoleClaims.FirstAsync();
        db.RoleClaims.Remove(revoked);
        await db.SaveChangesAsync();
        int remaining = await db.RoleClaims.CountAsync();

        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        client.DefaultRequestHeaders.Add("x-tenant-identifier", tenantId);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tenants/seed-data", new { });

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"The removed seed route returned {response.StatusCode}.");
        Assert.Equal(remaining, await db.RoleClaims.CountAsync());
        Assert.DoesNotContain(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>(), endpoint => endpoint.RoutePattern.RawText == "/api/tenants/seed-data");
    }

    private static WebApplication BuildApp(PauseFirstSave? pause = null, bool rejectDefaultPassword = false)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://app.example.com",
            ["Urls:ApiBaseUrl"] = "https://api.example.com",
            ["ConnectionStrings:appDb"] = "Host=localhost;Database=unused-startup-test;Username=test;Password=test",
            ["SMTPEmailSettings:Host"] = "smtp.example.com",
            ["TickerQBasicAuth:Username"] = "startup-test-operator",
            ["TickerQBasicAuth:Password"] = "startup-test-private-dashboard-password",
            ["SecuritySettings:JwtSettings:Key"] = "startup-test-signing-key-at-least-32-characters",
            ["Bootstrap:RootPassword"] = BootstrapPassword,
            ["Authentication:Google:ClientId"] = "test-client",
            ["Authentication:Google:ClientSecret"] = "test-secret"
        });
        // Both web hosts call this registration. Retain its actual initializer, while replacing
        // external persistence/jobs and disabling the other workers for this isolated host.
        builder.AddBackendServices();
        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(DefaultTenantInitializer));
        foreach (ServiceDescriptor worker in builder.Services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                     descriptor.ImplementationType != typeof(DefaultTenantInitializer)).ToArray())
            builder.Services.Remove(worker);
        string database = Guid.NewGuid().ToString();
        builder.Services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        builder.Services.RemoveAll<DbContextOptions<TenantDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<TenantDbContext>>();
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase(database);
            if (pause is not null) options.AddInterceptors(pause);
        });
        builder.Services.AddDbContext<TenantDbContext>(options => options.UseInMemoryDatabase(database + "-tenants"));
        builder.Services.RemoveAll<IJobService>();
        builder.Services.AddSingleton<IdentityTestContext.RecordingJobService>();
        builder.Services.AddSingleton<IJobService>(provider => provider.GetRequiredService<IdentityTestContext.RecordingJobService>());
        if (rejectDefaultPassword)
            builder.Services.Configure<IdentityOptions>(options => options.Password.RequiredLength = 128);

        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseBackendMiddleware(builder.Configuration);
        app.MapDiscoveredRoutes();
        return app;
    }

    private static async Task AddRegistryAsync(WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        TenantDbContext registry = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        await registry.Database.EnsureCreatedAsync();
        registry.Tenants.Add(new Tenant
        {
            Id = "blue", Identifier = "blue", AdminEmail = "owner@example.com",
            IsActive = true, ValidUpto = DateTime.MaxValue
        });
        await registry.SaveChangesAsync();
    }

    private static void SetTenant(IServiceProvider services, string id) =>
        services.GetRequiredService<ITenantSetterService>().SetTenant(new CurrentTenantDetails
        {
            Id = id, Identifier = id, AdminEmail = "owner@example.com", TenantLink = $"https://{id}.example.com"
        });

    private sealed class PauseFirstSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is ApplicationDbContext && !Entered.Task.IsCompleted)
            {
                Entered.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
