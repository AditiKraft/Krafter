using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Backend.Common.Tenants;

public interface ITenantFinderService
{
    public Task<Response<Tenant>> Find(string? identifier);
}
