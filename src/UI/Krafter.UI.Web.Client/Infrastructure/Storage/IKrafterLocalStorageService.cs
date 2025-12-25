using Krafter.Api.Client.Models;

namespace Krafter.UI.Web.Client.Infrastructure.Storage;

public interface IKrafterLocalStorageService
{
    public Task ClearCacheAsync();

    public ValueTask<DateTime> GetAuthTokenExpiryDate();

    public ValueTask<DateTime> GetRefreshTokenExpiryDate();

    public ValueTask CacheAuthTokens(TokenResponse tokenResponse);

    public ValueTask<string?> GetCachedAuthTokenAsync();

    public ValueTask<string?> GetCachedRefreshTokenAsync();

    public ValueTask<ICollection<string>?> GetCachedPermissionsAsync();
}
