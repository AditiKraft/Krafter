using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;

namespace AditiKraft.Krafter.Backend.Features.Users.Common;

public interface IUserMutationService
{
    Task<Response> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<Response> UpdateAsync(string id, CreateUserRequest request, CancellationToken cancellationToken);
    Task<Response> UpdateEmailAsync(string id, string email, CancellationToken cancellationToken);
}
