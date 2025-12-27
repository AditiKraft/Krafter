using Krafter.Shared.Contracts.Auth;

namespace Krafter.UI.Web.Client.Infrastructure.Storage;

public interface IKrafterLocalStorageService
{
    Task ClearCacheAsync();

    ValueTask<DateTime> GetAuthTokenExpiryDate();

    ValueTask<DateTime> GetRefreshTokenExpiryDate();

    ValueTask CacheAuthTokens(TokenResponse tokenResponse);

    ValueTask<string?> GetCachedAuthTokenAsync();

    ValueTask<string?> GetCachedRefreshTokenAsync();

    ValueTask<ICollection<string>?> GetCachedPermissionsAsync();
}
