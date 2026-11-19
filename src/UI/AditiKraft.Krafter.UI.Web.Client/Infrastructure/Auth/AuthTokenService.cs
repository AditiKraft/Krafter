using System.IdentityModel.Tokens.Jwt;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

internal enum CurrentSessionStatus { Valid, Rejected, Unavailable, Superseded }

public sealed class AuthTokenService(
    IAuthApiService apiService,
    IAuthStorageService storage,
    TokenRefreshCoordinator coordinator)
{
    public long SessionVersion => coordinator.SessionVersion;

    // Rejection belongs to this tab's checked credential. Cookies and local storage are shared by all tabs.
    private string? rejectedToken;
    internal bool IsRejected(string? token) => token is not null && token == rejectedToken;
    internal event Action? CurrentSessionRejected;

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
        RefreshAsync(cancellationToken, finishRotation: false);

    public Task<bool> RefreshForRealtimeAsync(CancellationToken connectionCancellation) =>
        RefreshAsync(connectionCancellation, finishRotation: true).WaitAsync(connectionCancellation);

    private Task<bool> RefreshAsync(CancellationToken cancellationToken, bool finishRotation) =>
        coordinator.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Read again after waiting: another caller may have saved a refreshed token.
            string? token = await storage.GetCachedAuthTokenAsync().AsTask().WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!NeedsRefresh(token))
            {
                return true;
            }

            string? refreshToken = await storage.GetCachedRefreshTokenAsync().AsTask().WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            var request = new RefreshTokenRequest { Token = token, RefreshToken = refreshToken };
            // Once sent, a browser refresh can rotate the only valid proof. A realtime connection
            // may stop waiting, but its teardown must not abandon the shared session's rotation.
            using var deadline = finishRotation ? new CancellationTokenSource(TimeSpan.FromSeconds(100)) : null;
            CancellationToken exchangeCancellation = deadline?.Token ?? cancellationToken;
            Response<TokenResponse> response = await apiService.RefreshTokenAsync(request, exchangeCancellation)
                .WaitAsync(exchangeCancellation);
            exchangeCancellation.ThrowIfCancellationRequested();
            return await SaveTokensAsync(response);
        }, cancellationToken);

    public async Task<bool> SynchronizeFromServerAsync(CancellationToken cancellationToken = default) =>
        await CheckCurrentSessionAsync(cancellationToken) == CurrentSessionStatus.Valid;

    internal Task<CurrentSessionStatus> CheckCurrentSessionAsync(CancellationToken cancellationToken = default) =>
        coordinator.ExecuteAsync(async () =>
        {
            string? previous = await storage.GetCachedAuthTokenAsync();
            long version = SessionVersion;
            Response<TokenResponse>? response = await apiService.GetCurrentTokenAsync(cancellationToken);
            if (await storage.GetCachedAuthTokenAsync() != previous ||
                (response?.IsError == true && SessionVersion != version))
            {
                return CurrentSessionStatus.Superseded;
            }

            if (response is { IsError: true, StatusCode: 401 })
            {
                rejectedToken = previous;
                if (previous is not null) CurrentSessionRejected?.Invoke();
                return CurrentSessionStatus.Rejected;
            }

            if (!await SaveTokensAsync(response)) return CurrentSessionStatus.Unavailable;
            rejectedToken = null;
            return CurrentSessionStatus.Valid;
        }, cancellationToken);

    private async Task<bool> SaveTokensAsync(Response<TokenResponse>? response)
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
