using System.Security.Claims;
using System.Text.Json;
using AditiKraft.Krafter.Contracts.Common.Auth;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public class UIAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAuthenticationService _authenticationService;
    private readonly PersistentComponentState _persistentState;
    private readonly IAuthStorageService _localStorage;
    private readonly AuthTokenService _tokenService;
    private bool _isInitialLoad = true;

    public UIAuthenticationStateProvider(AuthTokenService tokenService, IAuthStorageService localStorage,
        IAuthenticationService authenticationService,
        PersistentComponentState persistentState)
    {
        _authenticationService = authenticationService;
        _persistentState = persistentState;
        _localStorage = localStorage;
        _tokenService = tokenService;

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
            await _tokenService.SynchronizeFromServerAsync();

            return new AuthenticationState(claimsPrincipal);
        }

        string? cachedToken = await _localStorage.GetCachedAuthTokenAsync();
        if (string.IsNullOrWhiteSpace(cachedToken))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        if (AuthTokenService.NeedsRefresh(cachedToken))
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
            claimsIdentity.AddClaims(cachedPermissions.Select(p => new Claim(AppClaimTypes.Permission, p)));
        }

        return new AuthenticationState(new ClaimsPrincipal(claimsIdentity));
    }

    private static ClaimsIdentity CreateIdentityFromUserInfo(UserInfo userInfo)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userInfo.Id),
            new(ClaimTypes.Email, userInfo.Email ?? string.Empty),
            new(ClaimTypes.GivenName, userInfo.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, userInfo.LastName ?? string.Empty),
            new(AppClaimTypes.Fullname, $"{userInfo.FirstName} {userInfo.LastName}")
        };

        claims.AddRange(userInfo.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(userInfo.Permissions.Select(p => new Claim(AppClaimTypes.Permission, p)));

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
