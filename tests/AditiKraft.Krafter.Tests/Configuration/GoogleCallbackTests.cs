using System.Net;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Web.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class GoogleCallbackTests
{
    [Fact]
    public async Task ProductionGoogleClientDoesNotRetryAOneUseExchangeAfterProviderFailure()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://app.example.test",
            ["Authentication:Google:ClientId"] = "test-client",
            ["Authentication:Google:ClientSecret"] = "test-secret"
        });
        builder.AddServiceDefaults();
        builder.Services.AddAuthServices(builder.Configuration);
        var transport = new FailingProvider();
        builder.Services.AddHttpClient<ExternalAuth.GoogleAuthClient>().ConfigurePrimaryHttpMessageHandler(() => transport);
        using IHost host = builder.Build();
        ExternalAuth.GoogleAuthClient client = host.Services.GetRequiredService<ExternalAuth.GoogleAuthClient>();
        Assert.Null(await client.ExchangeCodeForTokensAsync("provider-code", new string('v', 43)));
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("invalid verifier containing spaces invalid verifier containing spaces")]
    public async Task ExchangeWithoutAValidStoredPkceVerifierNeverCallsGoogle(string verifier)
    {
        using var handler = new TokenRequestHandler();
        using var http = new HttpClient(handler);
        var client = new ExternalAuth.GoogleAuthClient(http, new ConfigurationBuilder().Build(),
            NullLogger<ExternalAuth.GoogleAuthClient>.Instance);
        Assert.Null(await client.ExchangeCodeForTokensAsync("provider-code", verifier));
        Assert.Null(handler.RequestBody);
    }

    [Theory]
    [InlineData("https://app.example.com", "https://app.example.com/google-callback")]
    [InlineData("http://localhost:5100", "http://localhost:5100/google-callback")]
    public async Task TokenExchangeUsesRootUiCallbackAsync(string rootUiUrl, string expectedRedirectUri)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = rootUiUrl,
            ["Authentication:Google:ClientId"] = "test-client",
            ["Authentication:Google:ClientSecret"] = "test-secret"
        }).Build();
        using var handler = new TokenRequestHandler();
        using var httpClient = new HttpClient(handler);
        var googleClient = new ExternalAuth.GoogleAuthClient(
            httpClient, configuration, NullLogger<ExternalAuth.GoogleAuthClient>.Instance);

        string verifier = new('v', 43);
        ExternalAuth.GoogleAuthClient.GoogleTokens? tokens = await googleClient.ExchangeCodeForTokensAsync("test-code", verifier);

        Assert.NotNull(tokens);
        Assert.Equal("access-token", tokens.AccessToken);
        var parameters = QueryHelpers.ParseQuery(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal(expectedRedirectUri, parameters["redirect_uri"].ToString());
        Assert.Equal("test-client", parameters["client_id"].ToString());
        Assert.Equal("test-code", parameters["code"].ToString());
        Assert.Equal(verifier, parameters["code_verifier"].ToString());
    }

    private sealed class TokenRequestHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri?.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"access-token","id_token":"id-token"}""")
            };
        }
    }

    private sealed class FailingProvider : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }
    }
}
