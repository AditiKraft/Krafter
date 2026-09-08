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
