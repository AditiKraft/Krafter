using Backend.Common.Models;
using Backend.Features.Users._Shared;

namespace Backend.Features.Auth._Shared;

public interface ITokenService
{
    public Task<TokenResponse> GenerateTokensAndUpdateUser(string userId, string ipAddress);
    public Task<TokenResponse> GenerateTokensAndUpdateUser(KrafterUser user, string ipAddress);
}
