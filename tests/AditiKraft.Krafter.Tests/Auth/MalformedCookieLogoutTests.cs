using System.IdentityModel.Tokens.Jwt;
using System.Net;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class MalformedCookieLogoutTests
{
    [Theory]
    [InlineData(BlazorHostingMode.SplitHost)]
    [InlineData(BlazorHostingMode.SingleHost)]
    public async Task RealLogoutRouteClearsMissingMalformedAndOversizedCookies(BlazorHostingMode mode)
    {
        int oversizedLength = new JwtSecurityTokenHandler().MaximumTokenSizeInBytes + 1;
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        { ApplicationName = typeof(MalformedCookieLogoutTests).Assembly.GetName().Name, EnvironmentName = "Production" });
        // Exercise the application boundary even when a deployment permits large request headers.
        builder.WebHost.UseKestrel(options => options.Limits.MaxRequestHeadersTotalSize = oversizedLength + 4096)
            .UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHybridCache();
        builder.Services.AddScoped<IAuthStorageService, AuthStorageServiceServer>();
        using var backend = new HttpClient(new NoBackendCalls()) { BaseAddress = new Uri("https://backend.example.test") };
        builder.Services.AddSingleton(RestService.For<IAuthApi>(backend));
        builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
        await using WebApplication app = builder.Build();
        if (mode == BlazorHostingMode.SplitHost) app.MapAuthTokenBffEndpoints();
        else app.MapClientSessionEndpoints();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (string? token in new[] { null, "not-a-jwt", "eyJhbGciOiJIUzI1NiJ9.bm90LWpzb24.signature", new string('a', oversizedLength) })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tokens/logout");
                request.Headers.Add("Cookie", $"{StorageConstants.Local.RefreshToken}=placeholder" +
                    (token is null ? "" : $"; {StorageConstants.Local.AuthToken}={token}"));

                using HttpResponseMessage response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                string[] deleted = response.Headers.GetValues("Set-Cookie").ToArray();
                Assert.Equal(5, deleted.Length);
                foreach (string name in new[]
                {
                    StorageConstants.Local.AuthToken, StorageConstants.Local.RefreshToken, StorageConstants.Local.Permissions,
                    StorageConstants.Local.AuthTokenExpiryDate, StorageConstants.Local.RefreshTokenExpiryDate
                })
                    Assert.Contains(deleted, cookie => cookie.StartsWith(name + "=;", StringComparison.Ordinal));
            }
        }
        finally { await app.StopAsync(); }
    }

    private sealed class NoBackendCalls : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Signing out must clear cookies without a backend request.");
    }
}
