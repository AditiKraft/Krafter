using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using Blazored.LocalStorage;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;

public class KrafterLocalStorageService(ILocalStorageService localStorageService) : IKrafterLocalStorageService
{
    public async Task ClearCacheAsync()
    {
        await localStorageService.RemoveItemAsync(StorageConstants.Local.AuthToken);
        await localStorageService.RemoveItemAsync(StorageConstants.Local.RefreshToken);
        await localStorageService.RemoveItemAsync(StorageConstants.Local.Permissions);
        await localStorageService.RemoveItemAsync(StorageConstants.Local.AuthTokenExpiryDate);
        await localStorageService.RemoveItemAsync(StorageConstants.Local.RefreshTokenExpiryDate);
    }

    public async ValueTask<string?> GetCachedAuthTokenAsync() =>
        await localStorageService.GetItemAsync<string>(StorageConstants.Local.AuthToken);

    public async ValueTask<string?> GetCachedRefreshTokenAsync() =>
        await localStorageService.GetItemAsync<string>(StorageConstants.Local.RefreshToken);

    public async ValueTask<ICollection<string>?> GetCachedPermissionsAsync()
    {
        ValueTask<ICollection<string>?> permissions =
            localStorageService.GetItemAsync<ICollection<string>>(StorageConstants.Local.Permissions);
        return await permissions;
    }

    public async ValueTask CacheAuthTokens(TokenResponse tokenResponse)
    {
        await localStorageService.SetItemAsync(StorageConstants.Local.AuthToken, tokenResponse.Token);
        await localStorageService.SetItemAsync(StorageConstants.Local.RefreshToken, tokenResponse.RefreshToken);
        await localStorageService.SetItemAsync(StorageConstants.Local.AuthTokenExpiryDate,
            tokenResponse.TokenExpiryTime);
        await localStorageService.SetItemAsync(StorageConstants.Local.RefreshTokenExpiryDate,
            tokenResponse.RefreshTokenExpiryTime);
        if (tokenResponse.Permissions == null)
        {
            await localStorageService.RemoveItemAsync(StorageConstants.Local.Permissions);
        }
        else
        {
            await localStorageService.SetItemAsync(StorageConstants.Local.Permissions, tokenResponse.Permissions);
        }
    }

    public async ValueTask<DateTime> GetAuthTokenExpiryDate()
    {
        DateTime? expiry =
            await localStorageService.GetItemAsync<DateTime?>(StorageConstants.Local.AuthTokenExpiryDate);
        return expiry ?? DateTime.UtcNow.AddMinutes(-1);
    }

    public async ValueTask<DateTime> GetRefreshTokenExpiryDate()
    {
        DateTime? expiry =
            await localStorageService.GetItemAsync<DateTime?>(StorageConstants.Local.RefreshTokenExpiryDate);
        return expiry ?? DateTime.UtcNow.AddMinutes(-1);
    }
}
