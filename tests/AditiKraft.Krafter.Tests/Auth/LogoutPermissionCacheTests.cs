using System.Net;
using System.Security.Claims;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class LogoutPermissionCacheTests
{
    [Theory]
    [InlineData(BlazorHostingMode.SplitHost, false)]
    [InlineData(BlazorHostingMode.SplitHost, true)]
    [InlineData(BlazorHostingMode.SingleHost, false)]
    [InlineData(BlazorHostingMode.SingleHost, true)]
    public async Task AnonymousLogoutClearsForgedCookiesWithoutEvictingRootPermissions(BlazorHostingMode mode, bool expired)
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        { ApplicationName = typeof(LogoutPermissionCacheTests).Assembly.GetName().Name, EnvironmentName = "Production" });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
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
        HybridCache cache = app.Services.GetRequiredService<HybridCache>();
        List<string> rootPermissions = ["Permissions.Tenants.View"];
        await cache.SetAsync(PermissionsKey("root"), rootPermissions);
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false })
            { BaseAddress = new Uri(app.Urls.Single()) };
            // This unsigned token is readable, but the request has no authenticated principal.
            string forged = TokenTestData.Create("root", expired).Token;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tokens/logout");
            request.Headers.Add("Cookie", $"{StorageConstants.Local.AuthToken}={forged}; " +
                $"{StorageConstants.Local.RefreshToken}=untrusted-placeholder");

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(5, response.Headers.GetValues("Set-Cookie").Count());
            Assert.Equal(rootPermissions, await ReadPermissionsAsync(cache, "root"));
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData(true, "alice", "alice")]
    [InlineData(true, "alice", "root")]
    [InlineData(false, "alice", "root")]
    [InlineData(true, null, "root")]
    public async Task SharedCacheEvictionUsesOnlyTheAuthenticatedPrincipal(bool authenticated, string? principalId, string cookieId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();
        List<string> alicePermissions = ["Permissions.Users.View"];
        List<string> rootPermissions = ["Permissions.Tenants.View"];
        await cache.SetAsync(PermissionsKey("alice"), alicePermissions);
        await cache.SetAsync(PermissionsKey("root"), rootPermissions);
        var context = new DefaultHttpContext
        {
            // Model the identity supplied by authentication middleware independently of cookie text.
            User = new ClaimsPrincipal(new ClaimsIdentity(
                principalId is null ? [] : [new Claim(ClaimTypes.NameIdentifier, principalId)],
                authenticated ? "validated-test-principal" : null))
        };
        context.Request.Headers.Cookie = $"{StorageConstants.Local.AuthToken}={TokenTestData.Create(cookieId).Token}; " +
            $"{StorageConstants.Local.RefreshToken}=untrusted-placeholder";
        var storage = new AuthStorageServiceServer(new HttpContextAccessor { HttpContext = context }, cache);

        await storage.ClearCacheAsync();

        Assert.Equal(5, context.Response.Headers.SetCookie.Count);
        Assert.Null(await storage.GetCachedAuthTokenAsync());
        Assert.Null(await storage.GetCachedRefreshTokenAsync());
        Assert.Empty((await storage.GetCachedPermissionsAsync())!);
        if (authenticated && principalId == "alice")
            Assert.Empty(await ReadPermissionsAsync(cache, "alice"));
        else
            Assert.Equal(alicePermissions, await ReadPermissionsAsync(cache, "alice"));
        Assert.Equal(rootPermissions, await ReadPermissionsAsync(cache, "root"));
    }

    private static string PermissionsKey(string userId) => $"{StorageConstants.Local.Permissions}_{userId}";

    private static ValueTask<List<string>> ReadPermissionsAsync(HybridCache cache, string userId) =>
        cache.GetOrCreateAsync(PermissionsKey(userId), _ => ValueTask.FromResult(new List<string>()));

    private sealed class NoBackendCalls : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Cookie cleanup must not require a backend request.");
    }
}
