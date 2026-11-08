using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth;

[Headers("X-Krafter-Mfa: 1")]
public interface ITwoFactorApi
{
    [Get("/api/two-factor/status")]
    Task<Response<TwoFactorStatusResponse>> StatusAsync(CancellationToken cancellationToken = default);
    [Post("/api/two-factor/setup")]
    Task<Response<TwoFactorSetupResponse>> SetupAsync([Body] TwoFactorManagementRequest request, CancellationToken cancellationToken = default);
    [Post("/api/two-factor/confirm")]
    Task<Response<RecoveryCodesResponse>> ConfirmAsync([Body] TwoFactorRequest request, CancellationToken cancellationToken = default);
    [Post("/api/two-factor/disable")]
    Task<Response> DisableAsync([Body] TwoFactorManagementRequest request, CancellationToken cancellationToken = default);
    [Post("/api/two-factor/recovery-codes")]
    Task<Response<RecoveryCodesResponse>> RegenerateAsync([Body] TwoFactorManagementRequest request, CancellationToken cancellationToken = default);
}
