using System.IdentityModel.Tokens.Jwt;
using Krafter.Shared.Contracts.Auth;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.Extensions.Caching.Hybrid;

namespace Krafter.UI.Web.Services;

public class KrafterLocalStorageServiceServer(IHttpContextAccessor httpContextAccessor, HybridCache cache)
    : IKrafterLocalStorageService
{
    public async Task ClearCacheAsync()
    {
        if (httpContextAccessor.HttpContext?.Response is null)
        {
            return;
        }

        string? userId = ExtractUserIdFromToken(await GetCachedAuthTokenAsync());
        httpContextAccessor.HttpContext.Response.Cookies.Delete(StorageConstants.Local.AuthToken);
        httpContextAccessor.HttpContext.Response.Cookies.Delete(StorageConstants.Local.RefreshToken);
        httpContextAccessor.HttpContext.Response.Cookies.Delete(StorageConstants.Local.Permissions);
        httpContextAccessor.HttpContext.Response.Cookies.Delete(StorageConstants.Local.AuthTokenExpiryDate);
        httpContextAccessor.HttpContext.Response.Cookies.Delete(StorageConstants.Local.RefreshTokenExpiryDate);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            string permissionsCacheKey = $"{StorageConstants.Local.Permissions}_{userId}";
            if (!string.IsNullOrEmpty(permissionsCacheKey))
            {
                await cache.RemoveAsync(permissionsCacheKey);
            }
        }
    }

    public async ValueTask<string?> GetCachedAuthTokenAsync()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(StorageConstants.Local.AuthToken,
                out object? freshData) == true
            && freshData is string freshDataString)
        {
            if (!string.IsNullOrWhiteSpace(freshDataString))
            {
                return freshDataString;
            }
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
                out object? freshData) == true
            && freshData is string freshDataString)
        {
            if (!string.IsNullOrWhiteSpace(freshDataString))
            {
                return freshDataString;
            }
        }

        if (httpContextAccessor.HttpContext?.Request is null)
        {
            return await ValueTask.FromResult<string?>(null);
        }

        string? token = httpContextAccessor.HttpContext.Request.Cookies[StorageConstants.Local.RefreshToken];
        if (string.IsNullOrEmpty(token))
        {
            return await ValueTask.FromResult<string>(null);
        }

        return await ValueTask.FromResult(token);
    }

    public async ValueTask<ICollection<string>?> GetCachedPermissionsAsync()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(StorageConstants.Local.Permissions,
                out object? freshPermissions) == true &&
            freshPermissions is List<string> permissionsFromTempHttpContextItem)
        {
            if (permissionsFromTempHttpContextItem.Count > 0)
            {
                return await ValueTask.FromResult<ICollection<string>?>(permissionsFromTempHttpContextItem);
            }
        }

        string? userId = ExtractUserIdFromToken(await GetCachedAuthTokenAsync());
        if (!string.IsNullOrWhiteSpace(userId))
        {
            string permissionsCacheKey = $"{StorageConstants.Local.Permissions}_{userId}";
            List<string> res = await cache.GetOrCreateAsync(permissionsCacheKey,
                cancel => ValueTask.FromResult(new List<string>()));
            return res;
        }

        if (httpContextAccessor.HttpContext?.Request is null)
        {
            return await ValueTask.FromResult<ICollection<string>?>(null);
        }

        string? token = httpContextAccessor.HttpContext.Request.Cookies[StorageConstants.Local.Permissions];
        if (string.IsNullOrEmpty(token))
        {
            return await ValueTask.FromResult<ICollection<string>?>(null);
        }

        return await ValueTask.FromResult<ICollection<string>?>(token.Split(',').ToList());
    }

    private string? ExtractUserIdFromToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

        // Read token without validation (we just need to extract the claims)
        JwtSecurityToken? jwtToken = handler.ReadJwtToken(token);

        // Try to get the user ID from standard claims
        string? userId = jwtToken.Claims.FirstOrDefault(c =>
            c.Type == "sub" ||
            c.Type == "nameid" ||
            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
            c.Type == "uid")?.Value;
        return userId;
    }

    public async ValueTask CacheAuthTokens(TokenResponse tokenResponse)
    {
        if (httpContextAccessor.HttpContext?.Response is null)
        {
            return;
        }

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

        if (!string.IsNullOrEmpty(authTokenExpiryDateString))
        {
            try
            {
                // Convert to long
                long ticks = long.Parse(authTokenExpiryDateString);
                var dateTime = new DateTime(ticks, DateTimeKind.Utc);
                return await ValueTask.FromResult(dateTime);
            }
            catch (FormatException)
            {
                // Cookie was corrupted, return default
                return DateTime.UtcNow.AddMinutes(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Invalid DateTime ticks, return default
                return DateTime.UtcNow.AddMinutes(-1);
            }
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

        if (!string.IsNullOrEmpty(refreshTokenExpiryDateString))
        {
            try
            {
                // Convert to long
                long ticks = long.Parse(refreshTokenExpiryDateString);
                var dateTime = new DateTime(ticks, DateTimeKind.Utc);
                return await ValueTask.FromResult(dateTime);
            }
            catch (FormatException)
            {
                // Cookie was corrupted, return default
                return DateTime.UtcNow.AddMinutes(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Invalid DateTime ticks, return default
                return DateTime.UtcNow.AddMinutes(-1);
            }
        }

        // No cookie found, return expired time
        return DateTime.UtcNow.AddMinutes(-1);
    }
}
