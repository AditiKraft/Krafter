using System.Net;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorProxyTests
{
    [Theory]
    [InlineData("203.0.113.9", null, null, "203.0.113.9", "http")]
    [InlineData("127.0.0.1", null, null, "198.51.100.7", "https")]
    [InlineData("10.0.0.5", "10.0.0.5", null, "198.51.100.7", "https")]
    [InlineData("10.5.0.8", null, "10.5.0.0/24", "198.51.100.7", "https")]
    [InlineData("10.6.0.8", null, "10.5.0.0/24", "10.6.0.8", "http")]
    public async Task OnlyTrustedProxyHopsCanChangeTheClientAddress(string peer, string? proxy, string? network,
        string expectedAddress, string expectedScheme)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:KnownProxies:0"] = proxy,
            ["ReverseProxy:KnownNetworks:0"] = network
        }.Where(x => x.Value is not null));
        builder.AddServiceDefaults();
        using IHost host = builder.Build();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            host.Services.GetRequiredService<ILoggerFactory>(), host.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>());
        await middleware.Invoke(context);
        Assert.Equal(expectedAddress, context.Connection.RemoteIpAddress.ToString());
        Assert.Equal(expectedScheme, context.Request.Scheme);
    }

    [Fact]
    public async Task BffClientsKeepSeparateAddressesAndCannotForwardSpoofedHeaderText()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.example.com");
        context.Request.Headers["X-Forwarded-For"] = "attacker-controlled";
        var accessor = new HttpContextAccessor { HttpContext = context };
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IFormFactor>(new ServerFormFactor())
            .AddSingleton<IHttpContextAccessor>(accessor).BuildServiceProvider();
        var identifier = new TenantIdentifier(services, new AppUrls
        { RootUiUrl = "https://app.example.com", ApiBaseUrl = "https://api.example.com" });
        var transport = new RecordingTransport();
        using var client = new HttpClient(new RefitTenantHandler(identifier, true, accessor) { InnerHandler = transport });
        foreach (string address in new[] { "198.51.100.7", "198.51.100.8" })
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(address);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://placeholder.invalid/api/tokens");
            request.Headers.Add("X-Forwarded-For", "also-attacker-controlled");
            using HttpResponseMessage response = await client.SendAsync(request);
        }
        Assert.Equal(new[] { "198.51.100.7", "198.51.100.8" }, transport.Addresses);
        Assert.All(transport.Schemes, scheme => Assert.Equal("https", scheme));
    }

    private sealed class ServerFormFactor : IFormFactor
    {
        public string GetFormFactor() => "Web";
        public string GetPlatform() => "Test";
    }

    private sealed class RecordingTransport : HttpMessageHandler
    {
        public List<string> Addresses { get; } = [];
        public List<string> Schemes { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Addresses.Add(request.Headers.GetValues("X-Forwarded-For").Single());
            Schemes.Add(request.Headers.GetValues("X-Forwarded-Proto").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
