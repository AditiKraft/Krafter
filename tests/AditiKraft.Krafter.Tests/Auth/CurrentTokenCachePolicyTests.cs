using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Refit;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class CurrentTokenCachePolicyTests
{
    [Theory]
    [InlineData(BlazorHostingMode.SplitHost, "success", HttpStatusCode.OK)]
    [InlineData(BlazorHostingMode.SingleHost, "success", HttpStatusCode.OK)]
    [InlineData(BlazorHostingMode.SplitHost, "missing-token", HttpStatusCode.Unauthorized)]
    [InlineData(BlazorHostingMode.SingleHost, "missing-token", HttpStatusCode.Unauthorized)]
    [InlineData(BlazorHostingMode.SplitHost, "storage-error", HttpStatusCode.InternalServerError)]
    [InlineData(BlazorHostingMode.SingleHost, "storage-error", HttpStatusCode.InternalServerError)]
    [InlineData(BlazorHostingMode.SplitHost, "unauthenticated", HttpStatusCode.Unauthorized)]
    [InlineData(BlazorHostingMode.SingleHost, "unauthenticated", HttpStatusCode.Unauthorized)]
    [InlineData(BlazorHostingMode.SplitHost, "forbidden", HttpStatusCode.Forbidden)]
    [InlineData(BlazorHostingMode.SingleHost, "forbidden", HttpStatusCode.Forbidden)]
    public async Task CurrentCredentialsAndErrorsAreNeverCacheableAsync(BlazorHostingMode mode, string scenario, HttpStatusCode expected)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("current-token-cache-test-key-at-least-32-bytes"));
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-user") };
        if (scenario != "forbidden")
        {
            claims.Add(new Claim("session-access", "allowed"));
        }
        string accessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: claims, expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        var tokens = new TokenResponse(accessToken, "test-refresh-credential", DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddMinutes(29), []);
        var storage = new Storage(tokens, scenario);
        using var backend = new HttpClient(new SessionTransport()) { BaseAddress = new Uri("https://backend.example.test") };
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IAuthApi>(RestService.For<IAuthApi>(backend));
        builder.Services.AddSingleton<IAuthStorageService>(storage);
        builder.Services.AddHybridCache();
        builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, IssuerSigningKey = key, ClockSkew = TimeSpan.Zero
            };
            options.Events = new BlazorJwtBearerEvents(mode, _ => Task.FromResult(true));
        });
        builder.Services.AddAuthorization(options => options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().RequireClaim("session-access", "allowed").Build());
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AuthCookieMiddleware>();
        if (mode == BlazorHostingMode.SplitHost)
        {
            app.MapAuthTokenBffEndpoints();
        }
        else
        {
            app.MapClientSessionEndpoints();
        }
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            if (scenario != "unauthenticated")
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            foreach (string path in new[] { "/api/tokens/current", "/api/tokens/current/" })
            {
                using HttpResponseMessage response = await client.GetAsync(path);
                Assert.Equal(expected, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);
                Assert.False(response.Headers.Contains("Set-Cookie"));
                if (scenario == "success")
                {
                    Response<TokenResponse>? body = await response.Content.ReadFromJsonAsync<Response<TokenResponse>>();
                    Assert.Equal(tokens.Token, body?.Data?.Token);
                    Assert.Equal(tokens.RefreshToken, body?.Data?.RefreshToken);
                }
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class Storage(TokenResponse tokens, string scenario) : IAuthStorageService
    {
        public ValueTask<string?> GetCachedAuthTokenAsync() => ValueTask.FromResult<string?>(scenario == "missing-token" ? null : tokens.Token);
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult<string?>(scenario == "missing-token" ? null : tokens.RefreshToken);
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => ValueTask.FromResult(tokens.TokenExpiryTime);
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => scenario == "storage-error"
            ? throw new InvalidOperationException("Storage unavailable") : ValueTask.FromResult(tokens.RefreshTokenExpiryTime);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => ValueTask.FromResult<ICollection<string>?>([]);
        public ValueTask CacheAuthTokens(TokenResponse tokenResponse) => throw new NotSupportedException("Fresh current-token reads must not write credentials.");
        public Task ClearCacheAsync() => throw new NotSupportedException("This test does not log out.");
    }

    private sealed class SessionTransport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/api/tokens/validate", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Response<bool>.Success(true))
            });
        }
    }
}
