using Blazored.LocalStorage;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Common.Constants;

namespace Krafter.UI.Web.Client.Infrastructure.Storage;

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
        if (tokenResponse.Permissions == null)
        {
            await localStorageService.RemoveItemAsync(StorageConstants.Local.Permissions);
        }
        else
        {
            await localStorageService.SetItemAsync(StorageConstants.Local.Permissions, tokenResponse.Permissions);
        }
    }

    public ValueTask<DateTime> GetAuthTokenExpiryDate() => throw new NotImplementedException();

    public ValueTask<DateTime> GetRefreshTokenExpiryDate() => throw new NotImplementedException();
}
