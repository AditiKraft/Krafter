using System.Net;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class TenantUrlRoutingTests
{
    [Theory]
    [InlineData("https://app.example.com", "https://api.example.com", "root", "https://api.example.com")]
    [InlineData("https://blue.app.example.com", "https://api.example.com", "blue", "https://blue.api.example.com")]
    [InlineData("https://BLUE.app.example.com", "https://api.example.com:8443", "blue", "https://blue.api.example.com:8443")]
    [InlineData("https://app.example.com", null, "root", "https://app.example.com")]
    [InlineData("https://blue.app.example.com:8443", null, "blue", "https://blue.app.example.com:8443")]
    [InlineData("http://localhost:5002", "http://localhost:5001", "root", "http://localhost:5001")]
    [InlineData("http://192.168.1.10:5002", "http://192.168.1.10:5001", "root", "http://192.168.1.10:5001")]
    [InlineData("http://[::1]:5002", null, "root", "http://[::1]:5002")]
    [InlineData("http://192.168.1.10:5002", null, "root", "http://192.168.1.10:5002")]
    [InlineData("https://blue.app.example.com", "http://localhost:5001", "blue", "http://localhost:5001")]
    [InlineData("https://blue.app.example.com", "http://192.168.1.10:5001", "blue", "http://192.168.1.10:5001")]
    [InlineData("https://blue.app.example.com", "http://[::1]:5001", "blue", "http://[::1]:5001")]
    [InlineData("https://blue.unrelated.com", "https://api.example.com", "root", "https://api.example.com")]
    [InlineData("https://blue.app.example.com.attacker.com", "https://api.example.com", "root", "https://api.example.com")]
    [InlineData("https://nested.blue.app.example.com", "https://api.example.com", "root", "https://api.example.com")]
    public async Task BrowserRoutesUseHostingModeAndConfiguredRootDomain(
        string currentOrigin, string? apiOrigin, string expectedTenant, string expectedApiOrigin)
    {
        var urls = new AppUrls
        {
            RootUiUrl = "https://app.example.com",
            ApiBaseUrl = apiOrigin,
            ServerApiBaseUrl = "http://internal-api:8080"
        };
        using ServiceProvider provider = CreateProvider(currentOrigin, server: false);
        var identifier = new TenantIdentifier(provider, urls);
        var result = identifier.Get();

        Assert.False(result.isServerSide);
        Assert.Equal(expectedTenant, result.tenantIdentifier);
        Assert.Equal(expectedApiOrigin, result.backendUrl);
        Assert.Equal(new Uri(currentOrigin).GetLeftPart(UriPartial.Authority), result.clientBaseAddress);
        await AssertTransportAsync(identifier, expectedApiOrigin, expectedTenant);
        await AssertTransportAsync(identifier, currentOrigin, expectedTenant, isBffClient: true);
    }

    [Theory]
    [InlineData("https://app.example.com", "https://api.example.com", "http://internal-api:8080", "root", "http://internal-api:8080")]
    [InlineData("https://blue.app.example.com", "https://api.example.com", "http://internal-api:8080", "blue", "http://internal-api:8080")]
    [InlineData("https://blue.app.example.com", "https://api.example.com", null, "blue", "https://api.example.com")]
    [InlineData("https://blue.app.example.com", null, "http://localhost:5001", "blue", "http://localhost:5001")]
    [InlineData("https://blue.app.example.com", null, null, "blue", "https://app.example.com")]
    [InlineData("http://192.168.1.10:5002", "http://192.168.1.10:5001", null, "root", "http://192.168.1.10:5001")]
    public async Task ServerRoutesKeepConnectionAddressAndForwardTenantHeader(
        string currentOrigin, string? apiOrigin, string? serverOrigin, string expectedTenant, string expectedApiOrigin)
    {
        var urls = new AppUrls
        {
            RootUiUrl = "https://app.example.com",
            ApiBaseUrl = apiOrigin,
            ServerApiBaseUrl = serverOrigin
        };
        using ServiceProvider provider = CreateProvider(currentOrigin, server: true);
        var identifier = new TenantIdentifier(provider, urls);
        var result = identifier.Get();

        Assert.True(result.isServerSide);
        Assert.Equal(expectedTenant, result.tenantIdentifier);
        Assert.Equal(expectedApiOrigin, result.backendUrl);
        await AssertTransportAsync(identifier, expectedApiOrigin, expectedTenant);
    }

    [Theory]
    [InlineData("app.example.com", null, "root")]
    [InlineData("api.example.com", "blue", "blue")]
    [InlineData("api.example.com", null, "root")]
    [InlineData("blue.app.example.com", "other", "blue")]
    [InlineData("blue.api.example.com:8443", "other", "blue")]
    [InlineData("internal-api:8080", "blue", "blue")]
    [InlineData("192.168.1.10:5001", "blue", "blue")]
    [InlineData("[::1]:5001", "blue", "blue")]
    [InlineData("blue.unrelated.com", null, "root")]
    [InlineData("nested.blue.app.example.com", null, "root")]
    [InlineData("blue.api.example.com.attacker.com", null, "root")]
    public async Task BackendUsesConfiguredTenantDomainsBeforeHeader(string host, string? header, string expectedTenant)
    {
        await AssertBackendTenantAsync("https://app.example.com", host, header, expectedTenant);
    }

    [Fact]
    public async Task ApiRootBelowUiRootStillUsesTenantHeader()
    {
        await AssertBackendTenantAsync("https://example.com", "api.example.com", "blue", "blue");
    }

    [Fact]
    public async Task InternalServerHostUnderRootDomainPreservesActualRequestTenant()
    {
        const string serverOrigin = "https://api-internal.example.com";
        var urls = new AppUrls
        {
            RootUiUrl = "https://example.com",
            ApiBaseUrl = "https://api.example.com",
            ServerApiBaseUrl = serverOrigin
        };
        using ServiceProvider provider = CreateProvider("https://blue.example.com", server: true);
        var identifier = new TenantIdentifier(provider, urls);
        RecordingTransport transport = await AssertTransportAsync(identifier, serverOrigin, "blue");

        await AssertBackendTenantAsync(urls.RootUiUrl, transport.RequestUri!.Authority,
            transport.TenantHeader, "blue", serverOrigin);
    }

    private static async Task AssertBackendTenantAsync(string rootUiUrl, string host, string? header, string expectedTenant, string? serverOrigin = null)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = rootUiUrl,
            ["Urls:ApiBaseUrl"] = "https://api.example.com",
            ["Urls:ServerApiBaseUrl"] = serverOrigin
        }).Build();
        var finder = new RecordingTenantFinder();
        var currentTenant = new CurrentTenantService();
        var middleware = new MultiTenantServiceMiddleware(finder, currentTenant, new CurrentUser(), configuration);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        if (header is not null)
        {
            context.Request.Headers["x-tenant-identifier"] = header;
        }

        bool nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        Assert.Equal(expectedTenant, finder.Identifier);
        Assert.Equal(expectedTenant, currentTenant.Tenant.Identifier);
    }

    private static ServiceProvider CreateProvider(string origin, bool server)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFormFactor>(new TestFormFactor(server ? "Web" : "WebAssembly"));
        if (server)
        {
            var uri = new Uri(origin);
            var context = new DefaultHttpContext();
            context.Request.Scheme = uri.Scheme;
            context.Request.Host = HostString.FromUriComponent(uri);
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        }
        else
        {
            services.AddSingleton<NavigationManager>(new TestNavigationManager(origin));
        }

        return services.BuildServiceProvider();
    }

    private static async Task<RecordingTransport> AssertTransportAsync(TenantIdentifier identifier, string expectedOrigin, string expectedTenant, bool isBffClient = false)
    {
        var transport = new RecordingTransport();
        using var client = new HttpClient(new RefitTenantHandler(identifier, isBffClient) { InnerHandler = transport });
        using HttpResponseMessage response = await client.GetAsync("https://placeholder.invalid/users?page=2");
        Assert.Equal(new Uri(new Uri(expectedOrigin), "/users?page=2"), transport.RequestUri);
        Assert.Equal(expectedTenant, transport.TenantHeader);
        return transport;
    }

    private sealed class TestFormFactor(string name) : IFormFactor
    {
        public string GetFormFactor() => name;
        public string GetPlatform() => "Test";
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string origin) => Initialize(origin.TrimEnd('/') + "/", origin.TrimEnd('/') + "/users");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class RecordingTransport : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? TenantHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            TenantHeader = request.Headers.GetValues("x-tenant-identifier").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class RecordingTenantFinder : ITenantFinderService
    {
        public string? Identifier { get; private set; }

        public Task<Response<Tenant>> Find(string? identifier)
        {
            Identifier = identifier;
            return Task.FromResult(Response<Tenant>.Success(new Tenant
            {
                Id = identifier!,
                Identifier = identifier!,
                IsActive = true,
                ValidUpto = DateTime.MaxValue
            }));
        }
    }
}
