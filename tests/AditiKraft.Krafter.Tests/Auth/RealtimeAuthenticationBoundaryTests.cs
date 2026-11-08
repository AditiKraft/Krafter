using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using TickerQ.Dashboard.Hubs;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class RealtimeAuthenticationBoundaryTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("incorrect")]
    public async Task DashboardKeepsItsBasicAuthenticationWhileAppHubRequiresAValidSession(string proof)
    {
        string password = Guid.NewGuid().ToString();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["TickerQBasicAuth:Username"] = "test-operator", ["TickerQBasicAuth:Password"] = password });
        // Use the real production registrations, with background workers disabled in this isolated host.
        builder.Services.AddBackgroundJobs(builder.Configuration, builder.Environment);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddRealtimeServices();
        builder.Services.AddScoped<CurrentTenantService>();
        builder.Services.AddScoped<ITenantGetterService>(p => p.GetRequiredService<CurrentTenantService>());
        await using WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapHub<TickerQNotificationHub>("/dashboard-hub");
        // Leave endpoint authorization out so this assertion specifically verifies the session filter.
        app.MapHub<RealtimeHub>("/app-hub");
        await app.StartAsync();
        try
        {
            string credential = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-operator:" +
                (proof == "incorrect" ? "incorrect-proof" : password)));
            string query = proof == "missing" ? "" : "?auth=" + Uri.EscapeDataString(credential);
            await using HubConnection dashboard = Connection(app.Urls.Single() + "/dashboard-hub" + query);
            if (proof == "valid")
            {
                await dashboard.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
                await dashboard.InvokeAsync("JoinGroup", "test-group").WaitAsync(TimeSpan.FromSeconds(10));
            }
            else
            {
                Assert.NotNull(await Record.ExceptionAsync(async () =>
                {
                    await dashboard.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    await dashboard.InvokeAsync("JoinGroup", "test-group").WaitAsync(TimeSpan.FromSeconds(10));
                }));
            }
            await using HubConnection application = Connection(app.Urls.Single() + "/app-hub?auth=" + Uri.EscapeDataString(credential));
            Assert.NotNull(await Record.ExceptionAsync(async () =>
            {
                await application.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
                await application.InvokeAsync(nameof(RealtimeHub.SendMessageAsync), "test-user", "test-message")
                    .WaitAsync(TimeSpan.FromSeconds(10));
            }));
        }
        finally { await app.StopAsync(); }
    }

    private static HubConnection Connection(string url) => new HubConnectionBuilder().WithUrl(url,
        options => options.Transports = HttpTransportType.WebSockets).Build();
}
