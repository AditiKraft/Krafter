using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

public class AuthStorageServiceServer(IHttpContextAccessor httpContextAccessor, HybridCache cache)
    : IAuthStorageService
{
    public async Task ClearCacheAsync()
    {
        if (httpContextAccessor.HttpContext?.Response is null)
        {
            return;
        }

        string? token = await GetCachedAuthTokenAsync();
        HttpContext context = httpContextAccessor.HttpContext;
        context.Response.Cookies.Delete(StorageConstants.Local.AuthToken);
        context.Response.Cookies.Delete(StorageConstants.Local.RefreshToken);
        context.Response.Cookies.Delete(StorageConstants.Local.Permissions);
        context.Response.Cookies.Delete(StorageConstants.Local.AuthTokenExpiryDate);
        context.Response.Cookies.Delete(StorageConstants.Local.RefreshTokenExpiryDate);

        // The request cookies still contain the old credentials after response-cookie deletion.
        context.Items[StorageConstants.Local.AuthToken] = null;
        context.Items[StorageConstants.Local.RefreshToken] = null;
        context.Items[StorageConstants.Local.Permissions] = new List<string>();
        context.Items[StorageConstants.Local.AuthTokenExpiryDate] = DateTime.UtcNow.AddMinutes(-1);
        context.Items[StorageConstants.Local.RefreshTokenExpiryDate] = DateTime.UtcNow.AddMinutes(-1);

        string? userId = ExtractUserIdFromToken(token);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            string permissionsCacheKey = $"{StorageConstants.Local.Permissions}_{userId}";
            await cache.RemoveAsync(permissionsCacheKey);
        }
    }

    public async ValueTask<string?> GetCachedAuthTokenAsync()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(StorageConstants.Local.AuthToken,
                out object? freshData) == true)
        {
            return freshData as string;
        }

        if (httpContextAccessor.HttpContext?.Request is null)
        {
            return await ValueTask.FromResult<string?>(null);
        }

        string? token = httpContextAccessor.HttpContext.Request.Cookies[StorageConstants.Local.AuthToken];
        if (string.IsNullOrEmpty(token))
        {
            return await ValueTask.FromResult<string?>(null);
        }

        return await ValueTask.FromResult(token);
    }

    public async ValueTask<string?> GetCachedRefreshTokenAsync()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(StorageConstants.Local.RefreshToken,
                out object? freshData) == true)
        {
            return freshData as string;
        }

        if (httpContextAccessor.HttpContext?.Request is null)
        {
            return await ValueTask.FromResult<string?>(null);
        }

        string? token = httpContextAccessor.HttpContext.Request.Cookies[StorageConstants.Local.RefreshToken];
        if (string.IsNullOrEmpty(token))
        {
            return await ValueTask.FromResult<string?>(null);
        }

        return await ValueTask.FromResult(token);
    }

    public async ValueTask<ICollection<string>?> GetCachedPermissionsAsync()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(StorageConstants.Local.Permissions,
                out object? freshPermissions) == true &&
            freshPermissions is List<string> permissionsFromTempHttpContextItem)
        {
            return permissionsFromTempHttpContextItem;
        }

        string? userId = ExtractUserIdFromToken(await GetCachedAuthTokenAsync());
        if (!string.IsNullOrWhiteSpace(userId))
        {
            string permissionsCacheKey = $"{StorageConstants.Local.Permissions}_{userId}";
            List<string> res = await cache.GetOrCreateAsync(permissionsCacheKey,
                cancel => ValueTask.FromResult(new List<string>()));
            return res;
        }

        // Permissions come only from server-controlled state, never an unsigned browser cookie.
        return null;
    }

    private static string? ExtractUserIdFromToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        try
        {
            // This is best-effort cache-key extraction, never proof of authentication.
            JwtSecurityToken jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(c =>
                c.Type == "sub" ||
                c.Type == "nameid" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                c.Type == "uid")?.Value;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    public async ValueTask CacheAuthTokens(TokenResponse tokenResponse)
    {
        if (httpContextAccessor.HttpContext?.Response is null)
        {
            return;
        }

        HttpContext httpContext = httpContextAccessor.HttpContext;
        httpContext.Items[StorageConstants.Local.AuthToken] = tokenResponse.Token;
        httpContext.Items[StorageConstants.Local.RefreshToken] = tokenResponse.RefreshToken;
        httpContext.Items[StorageConstants.Local.AuthTokenExpiryDate] = tokenResponse.TokenExpiryTime;
        httpContext.Items[StorageConstants.Local.RefreshTokenExpiryDate] = tokenResponse.RefreshTokenExpiryTime;
        httpContext.Items[StorageConstants.Local.Permissions] = tokenResponse.Permissions;

        if (!httpContext.Response.HasStarted)
        {
            httpContextAccessor.HttpContext.Response.Cookies.Append(StorageConstants.Local.AuthToken, tokenResponse.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires =
                        tokenResponse
                            .RefreshTokenExpiryTime // So that even when the token expires, it will get a new token based on the refresh token and the cookie will always pass
                });

            httpContextAccessor.HttpContext.Response.Cookies.Append(StorageConstants.Local.RefreshToken,
                tokenResponse.RefreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = tokenResponse.RefreshTokenExpiryTime
                });

            if (tokenResponse.TokenExpiryTime is { } tokenExpiryTime)
            {
                httpContextAccessor.HttpContext.Response.Cookies.Append(StorageConstants.Local.AuthTokenExpiryDate,
                    tokenExpiryTime.Ticks.ToString(),
                    new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
            }

            if (tokenResponse.RefreshTokenExpiryTime is { } refreshTokenExpiry)
            {
                httpContextAccessor.HttpContext.Response.Cookies.Append(StorageConstants.Local.RefreshTokenExpiryDate,
                    refreshTokenExpiry.Ticks.ToString(),
                    new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
            }
        }

        string? userId = ExtractUserIdFromToken(tokenResponse.Token);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            string permissionsCacheKey = $"{StorageConstants.Local.Permissions}_{userId}";
            await cache.SetAsync(
                permissionsCacheKey,
                tokenResponse.Permissions,
                new HybridCacheEntryOptions { Expiration = tokenResponse.RefreshTokenExpiryTime - DateTime.UtcNow }
            );
        }
    }

    public async ValueTask<DateTime> GetAuthTokenExpiryDate()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(StorageConstants.Local.AuthTokenExpiryDate,
                out object? freshData) == true
            && freshData is DateTime freshDataDateTime)
        {
            if (freshDataDateTime != default)
            {
                return freshDataDateTime;
            }
        }

        if (httpContextAccessor.HttpContext?.Request is null)
        {
            return DateTime.UtcNow.AddMinutes(-1);
        }

        string? authTokenExpiryDateString =
            httpContextAccessor.HttpContext.Request.Cookies[StorageConstants.Local.AuthTokenExpiryDate];

        if (long.TryParse(authTokenExpiryDateString, out long ticks) &&
            ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
        {
            return await ValueTask.FromResult(new DateTime(ticks, DateTimeKind.Utc));
        }

        // No cookie found, return expired time
        return DateTime.UtcNow.AddMinutes(-1);
    }

    public async ValueTask<DateTime> GetRefreshTokenExpiryDate()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(StorageConstants.Local.RefreshTokenExpiryDate,
                out object? freshData) == true
            && freshData is DateTime freshDataDateTime)
        {
            if (freshDataDateTime != default)
            {
                return freshDataDateTime;
            }
        }

        if (httpContextAccessor.HttpContext?.Request is null)
        {
            return DateTime.UtcNow.AddMinutes(-1);
        }

        string? refreshTokenExpiryDateString =
            httpContextAccessor.HttpContext.Request.Cookies[StorageConstants.Local.RefreshTokenExpiryDate];

        if (long.TryParse(refreshTokenExpiryDateString, out long ticks) &&
            ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
        {
            return await ValueTask.FromResult(new DateTime(ticks, DateTimeKind.Utc));
        }

        // No cookie found, return expired time
        return DateTime.UtcNow.AddMinutes(-1);
    }
}
