using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Mapster;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public interface IGoogleAccountAuthenticator
{
    Task<Response<LoginResponse>> AuthenticateAsync(GoogleLoginState state, string code, CancellationToken cancellationToken);
}

public sealed class GoogleAccountAuthenticator(IServiceScopeFactory scopes) : IGoogleAccountAuthenticator
{
    public async Task<Response<LoginResponse>> AuthenticateAsync(GoogleLoginState state, string code, CancellationToken cancellationToken)
    {
        // The root callback's request scope may already contain a root-tenant DbContext. Restore the
        // recorded, authoritative tenant in a fresh scope before resolving Identity or application data.
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        Response<Tenant> resolved = await scope.ServiceProvider.GetRequiredService<ITenantFinderService>().Find(state.TenantIdentifier, cancellationToken);
        if (resolved.IsError || resolved.Data is not { IsActive: true, IsDeleted: false } tenant || tenant.Id != state.TenantId)
            return Response<LoginResponse>.Unauthorized("Unable to sign in.");
        CurrentTenantDetails current = tenant.Adapt<CurrentTenantDetails>();
        current.TenantLink = state.UiOrigin;
        current.Host = state.UiOrigin;
        scope.ServiceProvider.GetRequiredService<ITenantSetterService>().SetTenant(current);
        return await scope.ServiceProvider.GetRequiredService<ExternalAuth.Handler>()
            .AuthenticateCodeAsync(code, state.CodeVerifier, cancellationToken);
    }
}
