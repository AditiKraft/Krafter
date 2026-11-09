using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class RealtimeTransportTests
{
    [Theory]
    [InlineData(HttpTransportType.WebSockets, false, false)]
    [InlineData(HttpTransportType.ServerSentEvents, false, false)]
    [InlineData(HttpTransportType.LongPolling, false, false)]
    [InlineData(HttpTransportType.WebSockets, true, false)]
    [InlineData(HttpTransportType.ServerSentEvents, true, false)]
    [InlineData(HttpTransportType.LongPolling, true, false)]
    [InlineData(HttpTransportType.LongPolling, false, true)]
    public async Task AllAdvertisedTransportsPreserveTenantGroupsAndRevokeSessions(HttpTransportType transport, bool root, bool periodicTenantRevocation)
    {
        using var harness = new MfaHarness();
        string tenantId = root ? SeedDataConstants.RootTenant.Id : "transport-tenant-id";
        string identifier = root ? "root" : "blue";
        using MfaScope seed = harness.Scope(tenantId);
        var tenants = seed.Services.GetRequiredService<TenantDbContext>();
        tenants.Tenants.Add(new Tenant
        { Id = tenantId, Identifier = identifier, Name = identifier, AdminEmail = "owner@example.com", IsActive = true, ValidUpto = DateTime.MaxValue });
        await tenants.SaveChangesAsync();
        var account = await seed.AddUserAsync();
        await RealtimePermissionTestData.GrantAsync(seed, account.Id);
        var login = await seed.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        var principal = MfaHarness.Principal(login.Data!.Tokens!);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Urls:RootUiUrl"] = "https://app.example.com", ["Urls:ApiBaseUrl"] = "https://api.example.com" });
        builder.Services.AddScoped<CurrentTenantService>();
        builder.Services.AddScoped<ITenantGetterService>(s => s.GetRequiredService<CurrentTenantService>());
        builder.Services.AddScoped<ITenantSetterService>(s => s.GetRequiredService<CurrentTenantService>());
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<ITenantFinderService>(new HarnessTenantFinder(harness));
        builder.Services.AddScoped<MultiTenantServiceMiddleware>();
        builder.Services.AddSingleton<SessionHubFilter>(s => new SessionHubFilter(
            harness.Services.GetRequiredService<IServiceScopeFactory>(), s.GetRequiredService<IHostApplicationLifetime>(),
            s.GetRequiredService<ILogger<SessionHubFilter>>()));
        builder.Services.AddSignalR(o => o.AddFilter<SessionHubFilter>());
        await using WebApplication app = builder.Build();
        // Supply an already authenticated test principal; exercise real tenant middleware and transports.
        app.Use((context, next) => { context.User = principal; return next(context); });
        app.UseMiddleware<MultiTenantServiceMiddleware>();
        app.MapHub<TransportHub>("/api/RealtimeHub", o => o.LongPolling.PollTimeout = TimeSpan.FromSeconds(1));
        await app.StartAsync();
        try
        {
            await using var connection = new HubConnectionBuilder().WithUrl(app.Urls.Single() + "/api/RealtimeHub", o =>
            { o.Transports = transport; o.Headers["x-tenant-identifier"] = identifier; }).Build();
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("GroupTenant-" + tenantId,
                await connection.InvokeAsync<string>(nameof(TransportHub.TenantGroup)).WaitAsync(TimeSpan.FromSeconds(10)));
            using MfaScope changed = harness.Scope(tenantId);
            if (periodicTenantRevocation)
            {
                var registry = changed.Services.GetRequiredService<TenantDbContext>();
                var tenant = (await registry.Tenants.FindAsync(tenantId))!;
                tenant.IsActive = false;
                await registry.SaveChangesAsync();
            }
            else
            {
                var user = (await changed.Users.FindByIdAsync(account.Id))!;
                user.AuthenticationVersion++;
                Assert.True((await changed.Users.UpdateAsync(user)).Succeeded);
                Exception? rejected = await Record.ExceptionAsync(async () =>
                    await connection.InvokeAsync<string>(nameof(TransportHub.TenantGroup)).WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.NotNull(rejected);
            }
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(12));
        }
        finally { await app.StopAsync(); }
    }

    public sealed class TransportHub(ILogger<RealtimeHub> logger) : RealtimeHub(logger)
    {
        public string TenantGroup() => (string)Context.Items["RealtimeTenantGroup"]!;
    }

    private sealed class HarnessTenantFinder(MfaHarness harness) : ITenantFinderService
    {
        public async Task<Response<Tenant>> Find(string? identifier, CancellationToken cancellationToken = default)
        {
            using MfaScope scope = harness.Scope();
            return await scope.Services.GetRequiredService<ITenantFinderService>().Find(identifier, cancellationToken);
        }
    }
}
