using System.IdentityModel.Tokens.Jwt;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public sealed class AuthTokenService(
    IAuthApiService apiService,
    IAuthStorageService storage,
    TokenRefreshCoordinator coordinator)
{
    public static bool NeedsRefresh(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ReadJwtToken(token).ValidTo <= DateTime.UtcNow.AddMinutes(1);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            return true;
        }
    }

    public Task<bool> RefreshAsync(CancellationToken cancellationToken = default) =>
        coordinator.ExecuteAsync(async () =>
        {
            // Read again after waiting: another caller may have saved a refreshed token.
            string? token = await storage.GetCachedAuthTokenAsync();
            if (!NeedsRefresh(token))
            {
                return true;
            }

            string? refreshToken = await storage.GetCachedRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            var request = new RefreshTokenRequest { Token = token, RefreshToken = refreshToken };
            Response<TokenResponse> response = await apiService.RefreshTokenAsync(request, cancellationToken);
            return await SaveTokensAsync(response);
        }, cancellationToken);

    public Task<bool> SynchronizeFromServerAsync(CancellationToken cancellationToken = default) =>
        coordinator.ExecuteAsync(async () =>
        {
            Response<TokenResponse> response = await apiService.GetCurrentTokenAsync(cancellationToken);
            return await SaveTokensAsync(response);
        }, cancellationToken);

    private async Task<bool> SaveTokensAsync(Response<TokenResponse> response)
    {
        if (response is not { IsError: false, Data: not null } ||
            string.IsNullOrWhiteSpace(response.Data.Token) ||
            string.IsNullOrWhiteSpace(response.Data.RefreshToken))
        {
            return false;
        }

        // Keep the lock until storage is updated, so waiting callers see the saved result.
        await storage.CacheAuthTokens(response.Data);
        return true;
    }
}
