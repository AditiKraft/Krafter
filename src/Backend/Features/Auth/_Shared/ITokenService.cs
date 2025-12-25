using Backend.Common.Models;
using Backend.Features.Users._Shared;

namespace Backend.Features.Auth.Token;

public interface ITokenService
{
    public Task<TokenResponse> GenerateTokensAndUpdateUser(string userId, string ipAddress);
    public Task<TokenResponse> GenerateTokensAndUpdateUser(KrafterUser user, string ipAddress);
}
