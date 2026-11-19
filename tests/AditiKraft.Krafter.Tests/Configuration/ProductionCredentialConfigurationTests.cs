using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class ProductionCredentialConfigurationTests
{
    private const string SampleKey = "ziKPKoP2YWMztBmxlNNCVRojuZWxjHlDAB6sPC6Uc8";
    private const string PrivateKey = "private-test-signing-key-at-least-32-bytes";
    private const string PrivateDashboardPassword = "private-test-dashboard-password";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    [InlineData(SampleKey)]
    [InlineData(" " + SampleKey + " ")]
    public async Task NonDevelopmentCompositionsRejectUnsafeSigningKeysBeforeStartup(string? key)
    {
        foreach (string environment in new[] { Environments.Production, Environments.Staging })
        foreach (bool combined in new[] { false, true })
        {
            WebApplicationBuilder builder = CreateBuilder(environment, key, combined);
            builder.AddBackendServices();
            if (combined)
                builder.Services.AddUiHostServices(builder.Configuration, BlazorHostingMode.SingleHost);
            await using WebApplication app = builder.Build();

            // This is the actual host startup validator, invoked without starting workers or opening a database.
            OptionsValidationException error = Assert.Throws<OptionsValidationException>(() =>
                app.Services.GetRequiredService<IStartupValidator>().Validate());
            Assert.Contains("SecuritySettings:JwtSettings:Key", error.Message);
            if (key is { Length: > 5 })
                Assert.DoesNotContain(key, error.Message);
        }
    }

    [Theory]
    [InlineData("Development", SampleKey)]
    [InlineData("Production", PrivateKey)]
    [InlineData("Staging", PrivateKey)]
    [InlineData("Production", "éééééééééééééééé")]
    public async Task PrivateProductionAndDevelopmentSampleKeysRemainUsable(string environment, string key)
    {
        foreach (bool combined in new[] { false, true })
        {
            WebApplicationBuilder builder = CreateBuilder(environment, key, combined);
            if (environment == Environments.Development)
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TickerQBasicAuth:Username"] = "admin",
                    ["TickerQBasicAuth:Password"] = "admin"
                });
            builder.AddBackendServices();
            if (combined)
                builder.Services.AddUiHostServices(builder.Configuration, BlazorHostingMode.SingleHost);
            await using WebApplication app = builder.Build();

            app.Services.GetRequiredService<IStartupValidator>().Validate();
        }
    }

    [Theory]
    [InlineData(null, PrivateDashboardPassword)]
    [InlineData(" ", PrivateDashboardPassword)]
    [InlineData("operator", null)]
    [InlineData("operator", " ")]
    [InlineData("admin", "admin")]
    [InlineData("operator", "admin")]
    [InlineData("operator", " ADMIN ")]
    public void NonDevelopmentCompositionsRequirePrivateDashboardCredentials(string? username, string? password)
    {
        foreach (string environment in new[] { Environments.Production, Environments.Staging })
        {
            WebApplicationBuilder builder = CreateBuilder(environment, PrivateKey, combined: false);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TickerQBasicAuth:Username"] = username,
                ["TickerQBasicAuth:Password"] = password
            });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.AddBackendServices());
            Assert.Contains("TickerQBasicAuth:Username", error.Message);
            Assert.Contains("TickerQBasicAuth:Password", error.Message);
            Assert.DoesNotContain(PrivateDashboardPassword, error.Message);
        }
    }

    private static WebApplicationBuilder CreateBuilder(string environment, string? key, bool combined)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://app.example.test",
            ["Urls:ApiBaseUrl"] = combined ? null : "https://api.example.test",
            ["ConnectionStrings:appDb"] = "Host=unused.example.test;Database=unused;Username=unused;Password=unused",
            ["SMTPEmailSettings:Host"] = "smtp.example.test",
            ["SecuritySettings:JwtSettings:Key"] = key,
            ["TickerQBasicAuth:Username"] = "operator",
            ["TickerQBasicAuth:Password"] = PrivateDashboardPassword,
            ["Authentication:Google:ClientId"] = "test-client",
            ["Authentication:Google:ClientSecret"] = "test-secret"
        });
        return builder;
    }
}
