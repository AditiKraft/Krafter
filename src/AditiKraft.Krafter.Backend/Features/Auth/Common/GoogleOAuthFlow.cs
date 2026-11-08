using System.Security.Cryptography;
using System.Text;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Enums;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.WebUtilities;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed record GoogleLoginState(string TenantId, string TenantIdentifier, string UiOrigin,
    string ReturnUrl, string ClientNonceHash, string CodeVerifier);

public sealed class GoogleOAuthFlow(IGoogleLoginTransactionStore transactions, ITenantFinderService tenants,
    IGoogleAccountAuthenticator authenticator, AppUrls urls, IConfiguration configuration, TimeProvider clock)
{
    public const string AuthorizationPurpose = "authorize";
    public const string HandoffPurpose = "handoff";

    public async Task<Response<GoogleLoginStartResponse>> StartAsync(GoogleLoginStartRequest request, CancellationToken cancellationToken)
    {
        if (!GoogleLoginTransactionStore.IsSecret(request.ClientNonceHash) ||
            !GoogleLoginTransactionStore.IsSecret(request.RootCorrelationHash) || request.ReturnUrl?.Length > 2048)
            return Response<GoogleLoginStartResponse>.BadRequest("Invalid Google login request.");
        (Tenant Tenant, Uri Origin)? destination = await ResolveOriginAsync(request.UiOrigin, cancellationToken);
        if (destination is null)
            return Response<GoogleLoginStartResponse>.BadRequest("Invalid Google login origin.");
        string? clientId = configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return Response<GoogleLoginStartResponse>.CustomError("Google login is unavailable.", 503);
        string verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var state = new GoogleLoginState(destination.Value.Tenant.Id!, destination.Value.Tenant.Identifier,
            destination.Value.Origin.GetLeftPart(UriPartial.Authority), LocalReturnPath.Normalize(request.ReturnUrl),
            request.ClientNonceHash.ToUpperInvariant(), verifier);
        DateTimeOffset expires = clock.GetUtcNow().AddMinutes(5);
        string id = await transactions.CreateAsync(AuthorizationPurpose, state.TenantId,
            request.RootCorrelationHash.ToUpperInvariant(), state, expires, cancellationToken);
        string authorization = QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth",
            new Dictionary<string, string?>
            {
                ["client_id"] = clientId, ["redirect_uri"] = urls.GetGoogleRedirectUri().AbsoluteUri,
                ["response_type"] = "code", ["scope"] = "email profile", ["state"] = id,
                ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
                ["code_challenge_method"] = "S256"
            });
        return Response<GoogleLoginStartResponse>.Success(new(id, authorization, expires));
    }

    public async Task<Response<GoogleLoginCallbackResponse>> CallbackAsync(GoogleLoginCallbackRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 4096)
            return CallbackRejected();
        GoogleLoginState? state = await transactions.TakeAsync<GoogleLoginState>(request.State,
            AuthorizationPurpose, request.RootCorrelation, null, cancellationToken);
        if (state is null) return CallbackRejected();
        // Recheck registration/eligibility after consent; a deleted/reassigned tenant must not inherit a flow.
        (Tenant Tenant, Uri Origin)? destination = await ResolveOriginAsync(state.UiOrigin, cancellationToken);
        if (destination is null || destination.Value.Tenant.Id != state.TenantId ||
            destination.Value.Tenant.Identifier != state.TenantIdentifier) return CallbackRejected();
        Response<LoginResponse> login = await authenticator.AuthenticateAsync(state, request.Code, cancellationToken);
        if (login.IsError || login.Data is null || (!login.Data.IsCompleted && !login.Data.RequiresTwoFactor))
            return CallbackRejected();
        string handoff = await transactions.CreateAsync(HandoffPurpose, state.TenantId, state.ClientNonceHash,
            login.Data, clock.GetUtcNow().AddMinutes(1), cancellationToken);
        // A fragment is not sent in HTTP requests or referrers. No Google code or credential leaves this callback.
        string completion = new Uri(destination.Value.Origin, "/google-complete").AbsoluteUri +
            "#handoff=" + Uri.EscapeDataString(handoff) + "&returnUrl=" + Uri.EscapeDataString(state.ReturnUrl);
        return Response<GoogleLoginCallbackResponse>.Success(new(completion));
    }

    public async Task<Response<LoginResponse>> CompleteAsync(GoogleAuthRequest request, string tenantId,
        CancellationToken cancellationToken)
    {
        LoginResponse? login = await transactions.TakeAsync<LoginResponse>(request.Handoff, HandoffPurpose,
            request.BrowserNonce, tenantId, cancellationToken);
        return login is null ? Response<LoginResponse>.Unauthorized("Google login expired. Please try again.") :
            Response<LoginResponse>.Success(login);
    }

    private async Task<(Tenant Tenant, Uri Origin)?> ResolveOriginAsync(string origin, CancellationToken cancellationToken)
    {
        Uri supplied;
        try { supplied = AppUrls.ParseOrigin(origin, nameof(origin)); }
        catch (Exception exception) when (exception is InvalidOperationException or UriFormatException or ArgumentException) { return null; }
        string? identifier;
        Uri root = urls.GetRootUiUri();
        if (SameOrigin(supplied, root)) identifier = DefaultTenantConstants.Identifier;
        else
        {
            if (TenantSettings.TenancyMode == TenancyMode.Single) return null;
            identifier = urls.GetUiTenantIdentifier(supplied.Host);
            if (identifier is null || urls.IsReservedTenantIdentifier(identifier)) return null;
        }
        Response<Tenant> result = await tenants.Find(identifier, cancellationToken);
        if (result.IsError || result.Data is not { IsActive: true, IsDeleted: false } tenant ||
            string.IsNullOrWhiteSpace(tenant.Id) || tenant.ValidUpto < clock.GetUtcNow().UtcDateTime ||
            !string.Equals(tenant.Identifier, identifier, StringComparison.OrdinalIgnoreCase)) return null;
        Uri canonical = urls.GetTenantUiUri(tenant.Identifier);
        return SameOrigin(supplied, canonical) ? (tenant, canonical) : null;
    }

    private static bool SameOrigin(Uri left, Uri right) => left.Scheme == right.Scheme && left.Port == right.Port &&
        string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase);
    private static Response<GoogleLoginCallbackResponse> CallbackRejected() =>
        Response<GoogleLoginCallbackResponse>.Unauthorized("Google login expired or could not be verified. Please try again.");
}
