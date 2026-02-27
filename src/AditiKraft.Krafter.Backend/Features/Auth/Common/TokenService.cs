using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public class TokenService(
    UserManager<KrafterUser> userManager,
    KrafterContext krafterContext,
    IUserService userService,
    IOptions<JwtSettings> jwtSettings,
    IOptions<SecuritySettings> securitySettings)
    : ITokenService, IScopedService
{
    private readonly SecuritySettings _securitySettings = securitySettings.Value;
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;


    public async Task<Response<TokenResponse>> GenerateTokensAndUpdateUser(string userId, string ipAddress)
    {
        KrafterUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Response<TokenResponse>.Unauthorized("Authentication Failed.");
        }

        return Response<TokenResponse>.Success(await GenerateTokensAndUpdateUser(user, ipAddress));
    }

    public async Task<TokenResponse> GenerateTokensAndUpdateUser(KrafterUser user, string ipAddress)
    {
        string token = GenerateJwt(user, ipAddress);
        bool newTone = false;
        UserRefreshToken? tokenResponse =
            await krafterContext.UserRefreshTokens.FirstOrDefaultAsync(x => x.UserId == user.Id);
        if (tokenResponse is null)
        {
            newTone = true;
            tokenResponse = new UserRefreshToken { UserId = user.Id };
        }

        tokenResponse.RefreshToken = GenerateRefreshToken();
        tokenResponse.RefreshTokenExpiryTime =
            DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationInDays).AddMinutes(-1);
        tokenResponse.TokenExpiryTime =
            DateTime.UtcNow.AddMinutes(_jwtSettings.TokenExpirationInMinutes).AddMinutes(-1);
        ;
        if (newTone)
        {
            krafterContext.UserRefreshTokens.Add(tokenResponse);
        }
        else
        {
            krafterContext.UserRefreshTokens.Update(tokenResponse);
        }

        Response<List<string>>? permissions = await userService.GetPermissionsAsync(user.Id, CancellationToken.None);
        await krafterContext.SaveChangesAsync();
        return new TokenResponse(token, tokenResponse.RefreshToken, tokenResponse.RefreshTokenExpiryTime,
            tokenResponse.TokenExpiryTime, permissions?.Data ?? new List<string>());
    }

    private string GenerateJwt(KrafterUser user, string ipAddress) =>
        GenerateEncryptedToken(GetSigningCredentials(), GetClaims(user, ipAddress));

    private IEnumerable<Claim> GetClaims(KrafterUser user, string ipAddress) =>
        new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(KrafterClaims.Fullname, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(KrafterClaims.IpAddress, ipAddress),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty)
        };

    private static string GenerateRefreshToken()
    {
        byte[] randomNumber = new byte[32];
        using var randomNumberGenerator = RandomNumberGenerator.Create();
        randomNumberGenerator.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private string GenerateEncryptedToken(SigningCredentials signingCredentials, IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.TokenExpirationInMinutes),
            signingCredentials: signingCredentials);
        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }


    private SigningCredentials GetSigningCredentials()
    {
        byte[] secret = Encoding.UTF8.GetBytes(_jwtSettings.Key);
        return new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
    }
}



