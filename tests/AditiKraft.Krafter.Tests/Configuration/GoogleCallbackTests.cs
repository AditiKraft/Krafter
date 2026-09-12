using System.Net;
using AditiKraft.Krafter.Backend.Features.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class GoogleCallbackTests
{
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

        ExternalAuth.GoogleAuthClient.GoogleTokens? tokens = await googleClient.ExchangeCodeForTokensAsync("test-code");

        Assert.NotNull(tokens);
        Assert.Equal("access-token", tokens.AccessToken);
        var parameters = QueryHelpers.ParseQuery(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal(expectedRedirectUri, parameters["redirect_uri"].ToString());
        Assert.Equal("test-client", parameters["client_id"].ToString());
        Assert.Equal("test-code", parameters["code"].ToString());
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
}
