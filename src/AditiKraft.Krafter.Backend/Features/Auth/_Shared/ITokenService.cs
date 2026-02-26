using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;

namespace AditiKraft.Krafter.Backend.Features.Auth._Shared;

public interface ITokenService
{
    public Task<Response<TokenResponse>> GenerateTokensAndUpdateUser(string userId, string ipAddress);
    public Task<TokenResponse> GenerateTokensAndUpdateUser(KrafterUser user, string ipAddress);
}
