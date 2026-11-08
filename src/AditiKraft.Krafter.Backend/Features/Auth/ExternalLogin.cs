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
using Microsoft.EntityFrameworkCore.Storage;
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
        GoogleAuthClient googleAuthClient) : IScopedHandler
    {
        public async Task<Response<LoginResponse>> AuthenticateCodeAsync(
            string code, string codeVerifier,
            CancellationToken cancellationToken)
        {
            // Get the auth info using the code
            GoogleAuthClient.GoogleTokens? tokens = await googleAuthClient.ExchangeCodeForTokensAsync(code, codeVerifier, cancellationToken);

            if (tokens == null)
            {
                return Response<LoginResponse>.Unauthorized("Invalid token");
            }

            // Get user info from Google
            GoogleAuthClient.GoogleUserInfo? userInfo = await googleAuthClient.GetUserInfoAsync(tokens.AccessToken, cancellationToken);
            if (userInfo is null || !userInfo.VerifiedEmail || string.IsNullOrWhiteSpace(userInfo.Email))
            {
                return Response<LoginResponse>.Unauthorized("Invalid user info");
            }

            // Find or create user based on email
            ApplicationUser? user = await userManager.FindByEmailAsync(userInfo.Email);
            if (user == null)
            {
                ApplicationRole? basic = await roleManager.FindByNameAsync(RoleConstants.Basic);
                if (basic is null)
                {
                    return Response<LoginResponse>.NotFound("Basic Role Not Found.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                // Identity saves the user immediately. Keep that write and Basic-role membership
                // atomic so cancellation cannot leave an incompletely provisioned Google account.
                await using IDbContextTransaction? provisioning = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(cancellationToken)
                    : null;
                user = new ApplicationUser
                {
                    IsActive = true,
                    FirstName = userInfo.GivenName,
                    LastName = userInfo.FamilyName,
                    Email = userInfo.Email,
                    EmailConfirmed = userInfo.VerifiedEmail,
                    PhoneNumber = userInfo.Email,
                    UserName = userInfo.Email,
                    Id = Guid.NewGuid().ToString()
                };
                if (string.IsNullOrWhiteSpace(user.UserName))
                {
                    user.UserName = user.Email;
                }

                IdentityResult result = await userManager.CreateAsync(user);
                cancellationToken.ThrowIfCancellationRequested();
                if (!result.Succeeded)
                {
                    return Response<LoginResponse>.BadRequest("An error occurred while creating user.");
                }

                db.UserRoles.Add(new ApplicationUserRole { RoleId = basic.Id, UserId = user.Id });
                await db.SaveChangesAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (provisioning is not null)
                {
                    // As with MFA transitions, finish commit once it starts. This transaction is
                    // disposed at the end of this block before TwoFactorService starts its own.
                    await provisioning.CommitAsync(CancellationToken.None);
                }
            }

            Response<LoginResponse> res = await twoFactor.ExternalLoginAsync(user.Id, string.Empty, cancellationToken);
            return res;
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



