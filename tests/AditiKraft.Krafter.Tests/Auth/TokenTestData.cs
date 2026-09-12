using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Tests.Auth;

internal static class TokenTestData
{
    public static TokenResponse Create(string userId = "alice", bool expired = false)
    {
        DateTime expiry = DateTime.UtcNow.AddMinutes(expired ? -5 : 30);
        var token = new JwtSecurityToken(
            claims: [new Claim("sub", userId)],
            expires: expiry);
        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            $"refresh-{userId}-{Guid.NewGuid()}",
            DateTime.UtcNow.AddDays(1),
            expiry,
            []);
    }
}
