using Backend.Api;
using Backend.Features.Auth._Shared;
using Backend.Features.Roles._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Auth;
using Krafter.Shared.Contracts.Roles;

namespace Backend.Features.Auth;

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

        public async Task<GoogleTokens?> ExchangeCodeForTokensAsync(string code)
        {
            string? clientId = _configuration["Authentication:Google:ClientId"];
            string? clientSecret = _configuration["Authentication:Google:ClientSecret"];
            string? redirectUri = _configuration["Authentication:Google:RedirectUri"];

            var tokenRequestParams = new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            };

            HttpResponseMessage response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(tokenRequestParams!));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error exchanging code for tokens: {StatusCode} {ReasonPhrase}",
                    response.StatusCode, response.ReasonPhrase);
                _logger.LogError("Response: {ResponseContent}",
                    await response.Content.ReadAsStringAsync());

                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleTokens>();
        }

        public async Task<GoogleUserInfo?> GetUserInfoAsync(string accessToken)
        {
            var requestMessage =
                new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            requestMessage.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleUserInfo>();
        }
    }

    internal sealed class Handler(
        ITokenService tokenService,
        KrafterContext db,
        UserManager<KrafterUser> userManager,
        RoleManager<KrafterRole> roleManager,
        GoogleAuthClient googleAuthClient) : IScopedHandler
    {
        public async Task<Response<TokenResponse>> GetTokenAsync(
            GoogleAuthRequest request,
            CancellationToken cancellationToken)
        {
            // Get the auth info using the code
            GoogleAuthClient.GoogleTokens? tokens = await googleAuthClient.ExchangeCodeForTokensAsync(request.Code);

            if (tokens == null)
            {
                return Response<TokenResponse>.Unauthorized("Invalid token");
            }

            // Get user info from Google
            GoogleAuthClient.GoogleUserInfo? userInfo = await googleAuthClient.GetUserInfoAsync(tokens.AccessToken);
            if (userInfo == null)
            {
                return Response<TokenResponse>.Unauthorized("Invalid user info");
            }

            // Find or create user based on email
            KrafterUser? user = await userManager.FindByEmailAsync(userInfo.Email);
            if (user == null)
            {
                KrafterRole? basic = await roleManager.FindByNameAsync(KrafterRoleConstant.Basic);
                if (basic is null)
                {
                    return Response<TokenResponse>.NotFound("Basic Role Not Found.");
                }

                user = new KrafterUser
                {
                    IsActive = true,
                    FirstName = userInfo.GivenName,
                    LastName = userInfo.Email,
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
                if (!result.Succeeded)
                {
                    return Response<TokenResponse>.BadRequest("An error occurred while creating user.");
                }

                db.UserRoles.Add(new KrafterUserRole { RoleId = basic.Id, UserId = user.Id });
                await db.SaveChangesAsync(new List<string>(), true, cancellationToken);
            }

            Response<TokenResponse> res = await tokenService.GenerateTokensAndUpdateUser(user.Id, string.Empty);
            return res;
        }
    }

    public sealed class GoogleAuthRoute : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder productGroup = app.MapGroup(KrafterRoute.ExternalAuth)
                .AddFluentValidationFilter();

            productGroup.MapPost("/google", async (
                HttpContext context,
                GoogleAuthRequest request,
                Handler externalAuthService,
                KrafterContext db,
                UserManager<KrafterUser> userManager,
                RoleManager<KrafterRole> roleManager) =>
            {
                Response<TokenResponse> res = await externalAuthService.GetTokenAsync(request, CancellationToken.None);
                return Results.Json(res, statusCode: res.StatusCode);
            }).AllowAnonymous();
        }
    }
}
