using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public interface ITokenService
{
    public Task<Response<TokenResponse>> GenerateTokensAndUpdateUser(string userId, string ipAddress);
    public Task<TokenResponse> GenerateTokensAndUpdateUser(ApplicationUser user, string ipAddress);

    /// <summary>
    /// Issues a fresh access token for <paramref name="user"/> without rotating the refresh token,
    /// returning the still-valid <paramref name="existing"/> refresh token as-is. Used to honour a
    /// concurrent refresh that raced against a rotation and is being served from the grace window.
    /// </summary>
    public Task<TokenResponse> GenerateAccessTokenWithoutRotation(ApplicationUser user, string ipAddress,
        UserRefreshToken existing);
}


