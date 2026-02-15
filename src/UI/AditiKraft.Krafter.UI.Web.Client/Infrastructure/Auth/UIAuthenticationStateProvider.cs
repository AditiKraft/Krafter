using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using AditiKraft.Krafter.Shared.Common.Auth;
using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth._Shared;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Api;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.AspNetCore.Components.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public class UIAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAuthenticationService _authenticationService;
    private readonly PersistentComponentState _persistentState;
    private readonly IKrafterLocalStorageService _localStorage;
    private readonly ILogger<UIAuthenticationStateProvider> _logger;
    private readonly IApiService _apiService;
    private bool _isInitialLoad = true;

    public UIAuthenticationStateProvider(IApiService apiService, IKrafterLocalStorageService localStorage,
        IAuthenticationService authenticationService,
        ILogger<UIAuthenticationStateProvider> logger,
        PersistentComponentState persistentState)
    {
        _authenticationService = authenticationService;
        _persistentState = persistentState;
        _localStorage = localStorage;
        _logger = logger;
        _apiService = apiService;

        authenticationService.LoginChange += name =>
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        };
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // On the very first load, try to get the user from the persisted state.
        if (_isInitialLoad && _persistentState.TryTakeFromJson<UserInfo>(nameof(UserInfo), out UserInfo? userInfo) &&
            userInfo is not null)
        {
            _isInitialLoad = false;
            var claimsPrincipal = new ClaimsPrincipal(CreateIdentityFromUserInfo(userInfo));

            // Sync tokens from server-side cookies to WebAssembly storage
            await SynchronizeTokensFromServerAsync();

            return new AuthenticationState(claimsPrincipal);
        }

        string? cachedToken = await _localStorage.GetCachedAuthTokenAsync();
        if (string.IsNullOrWhiteSpace(cachedToken))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        if (IsTokenExpired(cachedToken))
        {
            bool refreshResult = await _authenticationService.RefreshAsync();
            if (!refreshResult)
            {
                await _authenticationService.LogoutAsync("AuthenticationService Refresh Token 116");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            string? newCachedToken = await _localStorage.GetCachedAuthTokenAsync();
            if (string.IsNullOrWhiteSpace(newCachedToken))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            cachedToken = newCachedToken;
        }

        var claimsIdentity = new ClaimsIdentity(GetClaimsFromJwt(cachedToken), "jwt");

        if (await _localStorage.GetCachedPermissionsAsync() is List<string> cachedPermissions)
        {
            claimsIdentity.AddClaims(cachedPermissions.Select(p => new Claim(KrafterClaims.Permission, p)));
        }

        return new AuthenticationState(new ClaimsPrincipal(claimsIdentity));
    }

    private bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.ReadToken(token) is JwtSecurityToken jwtToken)
            {
                return jwtToken.ValidTo <= DateTime.UtcNow.AddMinutes(1);
            }

            return true;
        }
        catch
        {
            return true;
        }
    }

    private async Task SynchronizeTokensFromServerAsync()
    {
        await TokenSynchronizationManager.TryExecuteWithSynchronizationAsync(
            async () =>
            {
                _logger.LogInformation("Starting token synchronization from server");
                Response<TokenResponse>? tokenData = await _apiService.GetCurrentTokenAsync(CancellationToken.None);

                if (tokenData?.Data?.Token != null && tokenData.Data.RefreshToken != null)
                {
                    string? existingToken = await _localStorage.GetCachedAuthTokenAsync();
                    if (existingToken != tokenData.Data.Token)
                    {
                        _logger.LogInformation("Caching fresh tokens from server");
                        await _localStorage.CacheAuthTokens(tokenData.Data);
                    }

                    return tokenData.Data;
                }

                return null;
            },
            result => result != null,
            _logger
        );
    }

    private static ClaimsIdentity CreateIdentityFromUserInfo(UserInfo userInfo)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userInfo.Id),
            new(ClaimTypes.Email, userInfo.Email ?? string.Empty),
            new(ClaimTypes.GivenName, userInfo.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, userInfo.LastName ?? string.Empty),
            new(KrafterClaims.Fullname, $"{userInfo.FirstName} {userInfo.LastName}")
        };

        claims.AddRange(userInfo.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(userInfo.Permissions.Select(p => new Claim(KrafterClaims.Permission, p)));

        return new ClaimsIdentity(claims, "PersistentAuth");
    }

    private IEnumerable<Claim> GetClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        string payload = jwt.Split('.')[1];
        byte[] jsonBytes = ParseBase64WithoutPadding(payload);
        Dictionary<string, object>? keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs is not null)
        {
            keyValuePairs.TryGetValue(ClaimTypes.Role, out object? roles);

            if (roles is not null)
            {
                string? rolesString = roles.ToString();
                if (!string.IsNullOrEmpty(rolesString))
                {
                    if (rolesString.Trim().StartsWith("["))
                    {
                        string[]? parsedRoles = JsonSerializer.Deserialize<string[]>(rolesString);

                        if (parsedRoles is not null)
                        {
                            claims.AddRange(parsedRoles.Select(role => new Claim(ClaimTypes.Role, role)));
                        }
                    }
                    else
                    {
                        claims.Add(new Claim(ClaimTypes.Role, rolesString));
                    }
                }

                keyValuePairs.Remove(ClaimTypes.Role);
            }

            claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString() ?? string.Empty)));
        }

        return claims;
    }

    private byte[] ParseBase64WithoutPadding(string payload)
    {
        payload = payload.Trim().Replace('-', '+').Replace('_', '/');
        string base64 = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
