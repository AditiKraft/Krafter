using AditiKraft.Krafter.Backend.Web.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class CorsConfigurationTests
{
    [Theory]
    [InlineData("https://app.example.com", true)]
    [InlineData("https://APP.EXAMPLE.COM:443", true)]
    [InlineData("https://blue.app.example.com", true)]
    [InlineData("https://127.app.example.com", true)]
    [InlineData("https://blue-team.app.example.com", true)]
    [InlineData("https://deep.blue.app.example.com", false)]
    [InlineData("https://other.example.com", false)]
    [InlineData("https://app.example.com.attacker.com", false)]
    [InlineData("https://fakeapp.example.com", false)]
    [InlineData("http://app.example.com", false)]
    [InlineData("https://app.example.com:444", false)]
    [InlineData("http://blue.app.example.com", false)]
    [InlineData("https://blue.app.example.com:444", false)]
    [InlineData("https://blue.app.example.com/path", false)]
    [InlineData("https://blue.app.example.com?query=1", false)]
    [InlineData("https://blue.app.example.com#fragment", false)]
    [InlineData("https://user@app.example.com", false)]
    [InlineData("https://blue_team.app.example.com", false)]
    [InlineData("https://-blue.app.example.com", false)]
    [InlineData("https://blue-.app.example.com", false)]
    [InlineData("null", false)]
    [InlineData("not a URL", false)]
    [InlineData("ftp://app.example.com", false)]
    public async Task RootAndTenantOriginsRequireMatchingSchemeHostAndPortAsync(string origin, bool expected)
    {
        CorsPolicy policy = await CreatePolicyAsync("https://app.example.com", allowTenantSubdomains: true);

        Assert.Equal(expected, policy.IsOriginAllowed(origin));
        Assert.True(policy.SupportsCredentials);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task AdditionalOriginsAreExactAndTenantSubdomainsAreOptInAsync(string environment)
    {
        CorsPolicy policy = await CreatePolicyAsync("https://app.example.com", additionalSettings: new()
        {
            ["Cors:AllowedOrigins:0"] = "https://admin.example.net:8443"
        }, environmentName: environment);

        Assert.True(policy.IsOriginAllowed("https://app.example.com"));
        Assert.True(policy.IsOriginAllowed("https://admin.example.net:8443"));
        Assert.False(policy.IsOriginAllowed("https://blue.app.example.com"));
        Assert.False(policy.IsOriginAllowed("https://blue.admin.example.net:8443"));
        Assert.False(policy.IsOriginAllowed("https://admin.example.net"));
        Assert.False(policy.IsOriginAllowed("http://localhost:5000"));
        Assert.False(policy.IsOriginAllowed("https://attacker.example"));
    }

    [Theory]
    [InlineData("http://localhost:5000", "http://blue.localhost:5000")]
    [InlineData("http://127.0.0.1:5000", "http://blue.127.0.0.1:5000")]
    [InlineData("http://[::1]:5000", "http://blue.localhost:5000")]
    [InlineData("http://internal:5000", "http://blue.internal:5000")]
    public async Task LocalAndIpOriginsRequireExactMatchesAsync(string rootOrigin, string tenantOrigin)
    {
        CorsPolicy policy = await CreatePolicyAsync(rootOrigin, allowTenantSubdomains: true);

        Assert.True(policy.IsOriginAllowed(rootOrigin));
        Assert.False(policy.IsOriginAllowed(tenantOrigin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("app.example.com")]
    [InlineData("https://app.example.com/path")]
    public void InvalidRootConfigurationFailsDuringRegistration(string? rootOrigin)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = rootOrigin
        }).Build();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddCorsConfiguration(configuration));

        Assert.Contains("Urls:RootUiUrl", error.Message);
    }

    [Theory]
    [InlineData("*.example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://user@example.com")]
    public void InvalidAdditionalOriginFailsDuringRegistration(string origin)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://app.example.com",
            ["Cors:AllowedOrigins:0"] = origin
        }).Build();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddCorsConfiguration(configuration));

        Assert.Contains("Cors:AllowedOrigins:0", error.Message);
    }

    [Theory]
    [InlineData("https://krafter.getkrafter.dev", true)]
    [InlineData("https://blue.getkrafter.dev", true)]
    [InlineData("https://new-tenant.getkrafter.dev", true)]
    [InlineData("https://api.getkrafter.dev", false)]
    [InlineData("https://www.getkrafter.dev", false)]
    [InlineData("https://mail.getkrafter.dev", false)]
    [InlineData("https://support.getkrafter.dev", false)]
    [InlineData("https://blue.krafter.getkrafter.dev", false)]
    [InlineData("https://getkrafter.dev", false)]
    [InlineData("https://blue.getkrafter.dev.attacker.com", false)]
    [InlineData("http://blue.getkrafter.dev", false)]
    [InlineData("https://blue.getkrafter.dev:8443", false)]
    public async Task SiblingTenantCorsExcludesReservedAndUnrelatedOrigins(string origin, bool expected)
    {
        CorsPolicy policy = await CreatePolicyAsync("https://krafter.getkrafter.dev", true, new()
        {
            ["Urls:TenantBaseDomain"] = "getkrafter.dev",
            ["Urls:ApiBaseUrl"] = "https://api.getkrafter.dev",
            ["Urls:ReservedTenantIdentifiers:0"] = "support"
        });
        Assert.Equal(expected, policy.IsOriginAllowed(origin));
    }

    private static async Task<CorsPolicy> CreatePolicyAsync(
        string rootOrigin,
        bool allowTenantSubdomains = false,
        Dictionary<string, string?>? additionalSettings = null,
        string environmentName = "Production")
    {
        Dictionary<string, string?> settings = additionalSettings ?? [];
        settings["Urls:RootUiUrl"] = rootOrigin;
        settings["Cors:AllowTenantSubdomains"] = allowTenantSubdomains.ToString();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environmentName });
        services.AddCorsConfiguration(configuration);
        await using ServiceProvider provider = services.BuildServiceProvider();
        CorsPolicy? policy = await provider.GetRequiredService<ICorsPolicyProvider>()
            .GetPolicyAsync(new DefaultHttpContext(), "AllowSpecificOrigins");
        return Assert.IsType<CorsPolicy>(policy);
    }
}
