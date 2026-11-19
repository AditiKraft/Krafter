using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Mapster;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public interface IGoogleAccountAuthenticator
{
    Task<Response<GoogleAccountProof>> VerifyAsync(GoogleLoginState state, string code, CancellationToken cancellationToken);
    Task<Response<LoginResponse>> RedeemAsync(GoogleHandoff handoff, CancellationToken cancellationToken);
}

public sealed class GoogleAccountAuthenticator(IServiceScopeFactory scopes) : IGoogleAccountAuthenticator
{
    public Task<Response<GoogleAccountProof>> VerifyAsync(GoogleLoginState state, string code, CancellationToken cancellationToken) =>
        InTenantAsync(state.TenantId, state.TenantIdentifier, state.UiOrigin,
            handler => handler.VerifyCodeAsync(code, state.CodeVerifier, cancellationToken), cancellationToken);

    public Task<Response<LoginResponse>> RedeemAsync(GoogleHandoff handoff, CancellationToken cancellationToken) =>
        InTenantAsync(handoff.TenantId, handoff.TenantIdentifier, handoff.UiOrigin,
            handler => handler.RedeemAsync(handoff.Account, cancellationToken), cancellationToken);

    private async Task<Response<T>> InTenantAsync<T>(string tenantId, string identifier, string origin,
        Func<ExternalAuth.Handler, Task<Response<T>>> action, CancellationToken cancellationToken)
    {
        // Resolve tenant-scoped Identity and application data only after restoring the recorded tenant.
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        Response<Tenant> resolved = await scope.ServiceProvider.GetRequiredService<ITenantFinderService>().Find(identifier, cancellationToken);
        if (resolved.IsError || resolved.Data is not { IsActive: true, IsDeleted: false } tenant ||
            tenant.Id != tenantId || tenant.Identifier != identifier || tenant.ValidUpto < DateTime.UtcNow)
            return Response<T>.Unauthorized("Unable to sign in.");
        CurrentTenantDetails current = tenant.Adapt<CurrentTenantDetails>();
        current.TenantLink = origin;
        current.Host = origin;
        scope.ServiceProvider.GetRequiredService<ITenantSetterService>().SetTenant(current);
        return await action(scope.ServiceProvider.GetRequiredService<ExternalAuth.Handler>());
    }
}
