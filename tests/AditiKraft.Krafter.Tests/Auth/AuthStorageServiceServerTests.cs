using System.IdentityModel.Tokens.Jwt;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class AuthStorageServiceServerTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("invalid-payload")]
    [InlineData("oversized")]
    [InlineData("valid")]
    public async Task LogoutAlwaysDeletesCookiesAndClearsRequestLocalCredentials(string scenario)
    {
        TokenResponse tokens = TokenTestData.Create();
        string? token = scenario switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "invalid-payload" => "eyJhbGciOiJIUzI1NiJ9.bm90LWpzb24.signature",
            "oversized" => new string('a', new JwtSecurityTokenHandler().MaximumTokenSizeInBytes + 1),
            _ => tokens.Token
        };
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{StorageConstants.Local.RefreshToken}=placeholder; " +
            $"{StorageConstants.Local.AuthTokenExpiryDate}=9223372036854775808; " +
            $"{StorageConstants.Local.RefreshTokenExpiryDate}=9223372036854775808" +
            (token is null ? "" : $"; {StorageConstants.Local.AuthToken}={token}");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();
        string permissionsKey = $"{StorageConstants.Local.Permissions}_alice";
        await cache.SetAsync(permissionsKey, new List<string> { "Permissions.Users.View" });
        var storage = new AuthStorageServiceServer(new HttpContextAccessor { HttpContext = context }, cache);

        // Permission-cache lookups must also tolerate untrusted cookie tokens.
        ICollection<string>? before = await storage.GetCachedPermissionsAsync();
        if (scenario == "valid") Assert.Equal(["Permissions.Users.View"], before);
        else Assert.Null(before);

        await storage.ClearCacheAsync();

        Assert.Equal(5, context.Response.Headers.SetCookie.Count);
        foreach (string name in new[]
        {
            StorageConstants.Local.AuthToken, StorageConstants.Local.RefreshToken, StorageConstants.Local.Permissions,
            StorageConstants.Local.AuthTokenExpiryDate, StorageConstants.Local.RefreshTokenExpiryDate
        })
            Assert.Contains(context.Response.Headers.SetCookie, cookie => cookie!.StartsWith(name + "=;", StringComparison.Ordinal));
        Assert.Null(await storage.GetCachedAuthTokenAsync());
        Assert.Null(await storage.GetCachedRefreshTokenAsync());
        Assert.Empty((await storage.GetCachedPermissionsAsync())!);
        Assert.True(await storage.GetAuthTokenExpiryDate() < DateTime.UtcNow);
        Assert.True(await storage.GetRefreshTokenExpiryDate() < DateTime.UtcNow);
        if (scenario == "valid")
            Assert.Empty(await cache.GetOrCreateAsync(permissionsKey, _ => ValueTask.FromResult(new List<string>())));
    }

    [Fact]
    public async Task LogoutRemovesFreshCredentialsAndPermissionsSavedDuringTheSameRequest()
    {
        var context = new DefaultHttpContext();
        TokenResponse old = TokenTestData.Create("old-user");
        TokenResponse fresh = TokenTestData.Create() with { Permissions = ["Permissions.Users.View"] };
        context.Request.Headers.Cookie = $"{StorageConstants.Local.AuthToken}={old.Token}; " +
            $"{StorageConstants.Local.RefreshToken}={old.RefreshToken}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();
        var storage = new AuthStorageServiceServer(new HttpContextAccessor { HttpContext = context }, cache);
        await storage.CacheAuthTokens(fresh);

        await storage.ClearCacheAsync();

        Assert.Null(await storage.GetCachedAuthTokenAsync());
        Assert.Null(await storage.GetCachedRefreshTokenAsync());
        Assert.Empty((await storage.GetCachedPermissionsAsync())!);
        Assert.Empty(await cache.GetOrCreateAsync($"{StorageConstants.Local.Permissions}_alice",
            _ => ValueTask.FromResult(new List<string>())));
        Assert.Equal(old.Token, context.Request.Cookies[StorageConstants.Local.AuthToken]);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("-1")]
    [InlineData("3155378976000000000")]
    [InlineData("9223372036854775807")]
    [InlineData("9223372036854775808")]
    public async Task CorruptOrOverflowingExpiryCookiesAreTreatedAsExpired(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{StorageConstants.Local.AuthTokenExpiryDate}={value}; " +
            $"{StorageConstants.Local.RefreshTokenExpiryDate}={value}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        var storage = new AuthStorageServiceServer(new HttpContextAccessor { HttpContext = context },
            provider.GetRequiredService<HybridCache>());

        Assert.True(await storage.GetAuthTokenExpiryDate() < DateTime.UtcNow);
        Assert.True(await storage.GetRefreshTokenExpiryDate() < DateTime.UtcNow);
    }

    [Fact]
    public async Task PermissionOnlyCookiesAreNotAnAuthoritativePermissionSource()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{StorageConstants.Local.Permissions}=Permissions.Users.View";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        var storage = new AuthStorageServiceServer(new HttpContextAccessor { HttpContext = context },
            provider.GetRequiredService<HybridCache>());
        ICollection<string>? permissions = await storage.GetCachedPermissionsAsync();
        Assert.True(permissions is null || permissions.Count == 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedTokensAndEmptyPermissionsAreVisibleWithinTheCurrentRequest(bool responseStarted)
    {
        TokenResponse original = TokenTestData.Create(expired: true) with { Permissions = ["Permissions.Users.View"] };
        TokenResponse refreshed = TokenTestData.Create();
        var context = new DefaultHttpContext();
        if (responseStarted)
        {
            context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature
            {
                Headers = new HeaderDictionary { IsReadOnly = true }
            });
        }

        context.Request.Headers.Cookie = string.Join("; ",
            $"{StorageConstants.Local.AuthToken}={original.Token}",
            $"{StorageConstants.Local.RefreshToken}={original.RefreshToken}",
            $"{StorageConstants.Local.AuthTokenExpiryDate}={original.TokenExpiryTime.Ticks}",
            $"{StorageConstants.Local.RefreshTokenExpiryDate}={original.RefreshTokenExpiryTime.Ticks}",
            $"{StorageConstants.Local.Permissions}=Permissions.Users.View");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();
        await cache.SetAsync($"{StorageConstants.Local.Permissions}_alice", original.Permissions);
        var storage = new AuthStorageServiceServer(new HttpContextAccessor { HttpContext = context }, cache);
        Assert.Equal(original.Token, await storage.GetCachedAuthTokenAsync());
        Assert.Equal(original.Permissions, await storage.GetCachedPermissionsAsync());

        await storage.CacheAuthTokens(refreshed);

        Assert.Equal(refreshed.Token, await storage.GetCachedAuthTokenAsync());
        Assert.Equal(refreshed.RefreshToken, await storage.GetCachedRefreshTokenAsync());
        Assert.Equal(refreshed.TokenExpiryTime, await storage.GetAuthTokenExpiryDate());
        Assert.Equal(refreshed.RefreshTokenExpiryTime, await storage.GetRefreshTokenExpiryDate());
        Assert.Empty((await storage.GetCachedPermissionsAsync())!);
        Assert.Empty(await cache.GetOrCreateAsync(
            $"{StorageConstants.Local.Permissions}_alice",
            _ => ValueTask.FromResult(original.Permissions)));
        Assert.Equal(original.Token, context.Request.Cookies[StorageConstants.Local.AuthToken]);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
