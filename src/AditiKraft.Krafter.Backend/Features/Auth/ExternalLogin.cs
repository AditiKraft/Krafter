using System.Text.Json.Serialization;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Auth;

public sealed class ExternalAuth
{
    public class GoogleAuthClient
    {
        public class GoogleTokens
        {
            [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
            [JsonPropertyName("id_token")] public string IdToken { get; set; } = string.Empty;
        }

        public class GoogleUserInfo
        {
            [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
            [JsonPropertyName("verified_email")] public bool VerifiedEmail { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("given_name")] public string GivenName { get; set; } = string.Empty;
            [JsonPropertyName("family_name")] public string FamilyName { get; set; } = string.Empty;
        }

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleAuthClient> _logger;

        public GoogleAuthClient(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleAuthClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GoogleTokens?> ExchangeCodeForTokensAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
        {
            if (codeVerifier.Length is < 43 or > 128 || !codeVerifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                return null;
            string? clientId = _configuration["Authentication:Google:ClientId"];
            string? clientSecret = _configuration["Authentication:Google:ClientSecret"];
            AppUrls urls = _configuration.GetSection(AppUrls.SectionName).Get<AppUrls>() ?? new AppUrls();
            string redirectUri = urls.GetGoogleRedirectUri().AbsoluteUri;

            var tokenRequestParams = new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            };

            HttpResponseMessage response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(tokenRequestParams!), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error exchanging code for tokens: {StatusCode} {ReasonPhrase}",
                    response.StatusCode, response.ReasonPhrase);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleTokens>(cancellationToken);
        }

        public async Task<GoogleUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            var requestMessage =
                new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            requestMessage.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleUserInfo>(cancellationToken);
        }
    }

    internal sealed class Handler(
        TwoFactorService twoFactor,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        GoogleAuthClient googleAuthClient, TimeProvider clock) : IScopedHandler
    {
        public async Task<Response<GoogleAccountProof>> VerifyCodeAsync(
            string code, string codeVerifier,
            CancellationToken cancellationToken)
        {
            // Get the auth info using the code
            GoogleAuthClient.GoogleTokens? tokens = await googleAuthClient.ExchangeCodeForTokensAsync(code, codeVerifier, cancellationToken);

            if (tokens == null)
            {
                return Response<GoogleAccountProof>.Unauthorized("Invalid token");
            }

            // Get user info from Google
            GoogleAuthClient.GoogleUserInfo? userInfo = await googleAuthClient.GetUserInfoAsync(tokens.AccessToken, cancellationToken);
            if (userInfo is null || !userInfo.VerifiedEmail || string.IsNullOrWhiteSpace(userInfo.Email))
            {
                return Response<GoogleAccountProof>.Unauthorized("Invalid user info");
            }

            long authenticatedAt = clock.GetUtcNow().ToUnixTimeSeconds();
            ApplicationUser? user = await userManager.FindByEmailAsync(userInfo.Email);
            bool eligible = user is null
                ? await AvailableBasicRoleAsync(userInfo.Email, cancellationToken) is not null
                : await twoFactor.EligibleAsync(user, cancellationToken);
            if (!eligible)
                return Response<GoogleAccountProof>.Unauthorized("Unable to sign in.");

            // Capture the account observed for the verified email. Only redemption may provision
            // a user, supersede a login challenge or rotate a session's refresh credential.
            return Response<GoogleAccountProof>.Success(new(userInfo.Email, userInfo.GivenName, userInfo.FamilyName,
                authenticatedAt, user?.Id, user?.AuthenticationVersion ?? 0));
        }

        public async Task<Response<LoginResponse>> RedeemAsync(GoogleAccountProof verified, CancellationToken cancellationToken)
        {
            string? id = verified.UserId;
            if (id is null)
            {
                ApplicationRole? basic = await AvailableBasicRoleAsync(verified.Email, cancellationToken);
                if (basic is null)
                    return Response<LoginResponse>.Unauthorized("Unable to sign in.");

                var user = new ApplicationUser
                {
                    IsActive = true,
                    FirstName = verified.GivenName,
                    LastName = verified.FamilyName,
                    Email = verified.Email,
                    EmailConfirmed = true,
                    PhoneNumber = verified.Email,
                    UserName = verified.Email,
                    Id = Guid.NewGuid().ToString()
                };
                id = user.Id;
                // Identity saves immediately. Roll back both account and Basic membership on
                // failure, and clear rejected tracked state before any later caller can save it.
                await using var provisioning = await DomainTransaction.BeginAsync(db, cancellationToken);
                try
                {
                    IdentityResult result = await userManager.CreateAsync(user);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!result.Succeeded)
                        return Response<LoginResponse>.Unauthorized("Unable to sign in.");

                    db.UserRoles.Add(new ApplicationUserRole { RoleId = basic.Id, UserId = user.Id });
                    await db.SaveChangesAsync(cancellationToken);
                    await provisioning.CommitAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (IdentityConflict.IsUser(exception))
                {
                    // Two first sign-ins can pass all preflight/Identity checks together.
                    // Preserve the verified OAuth failure destination with a normal rejection.
                    cancellationToken.ThrowIfCancellationRequested();
                    return Response<LoginResponse>.Unauthorized("Unable to sign in.");
                }
            }

            return await twoFactor.ExternalLoginAsync(id, string.Empty, cancellationToken,
                authenticatedAt: verified.AuthenticatedAt, verifiedEmail: verified.Email, expectedVersion: verified.AuthenticationVersion);
        }

        private async Task<ApplicationRole?> AvailableBasicRoleAsync(string email, CancellationToken cancellationToken)
        {
            ApplicationRole? basic = await roleManager.FindByNameAsync(RoleConstants.Basic);
            if (basic is null || UserIdentityValidation.Validate(userManager, email, email).IsError)
                return null;

            // Soft-deleted identifiers are reserved. An account created since verification
            // must also never inherit a handoff issued while this address was unclaimed.
            string? normalizedEmail = userManager.NormalizeEmail(email);
            string? normalizedName = userManager.NormalizeName(email);
            bool reserved = await db.Users.IgnoreQueryFilters().AnyAsync(candidate => candidate.TenantId == basic.TenantId &&
                (candidate.NormalizedEmail == normalizedEmail || candidate.NormalizedUserName == normalizedName), cancellationToken);
            return reserved ? null : basic;
        }
    }

    public sealed class GoogleAuthRoute : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder productGroup = app.MapGroup(ApiRoutes.ExternalAuth)
                .AddFluentValidationFilter();

            productGroup.MapPost($"/{RouteSegment.Google}", async (
                HttpContext context,
                GoogleAuthRequest request,
                GoogleOAuthFlow flow,
                AditiKraft.Krafter.Backend.Common.Tenants.ITenantGetterService tenant) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                Response<LoginResponse> res = await flow.CompleteAsync(request, tenant.Tenant.Id ?? string.Empty, context.RequestAborted);
                return Results.Json(res, statusCode: res.StatusCode);
            }).AllowAnonymous().RequireRateLimiting("authentication");
        }
    }
}



