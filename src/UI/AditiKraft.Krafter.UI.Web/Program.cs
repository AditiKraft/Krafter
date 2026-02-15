using System.Text;
using AditiKraft.Krafter.Shared.Common;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.UI.Web.Client;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth._Shared;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Api;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Components;
using AditiKraft.Krafter.UI.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();
builder.AddRedisDistributedCache("cache");
builder.AddRedisOutputCache("cache");
builder.Services.AddHybridCache();
builder.Services.AddScoped<ServerAuthenticationHandler>();
builder.Services.AddSingleton<IFormFactor, FormFactorServer>();
builder.Services.AddScoped<IApiService, ServerSideApiService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        string jwtKey = builder.Configuration["Jwt:Key"] ??
                        throw new InvalidOperationException("JWT Key not configured");
        byte[] key = Encoding.ASCII.GetBytes(jwtKey);

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = async context =>
            {
                string? currentToken = context.Request.Cookies[StorageConstants.Local.AuthToken];
                string? refreshToken = context.Request.Cookies[StorageConstants.Local.RefreshToken];

                // skip refresh logic for api endpoints we need this only in the case of prerendered Blazor components
                if (context.Request.Path.StartsWithSegments($"/{KrafterRoute.Tokens}/{RouteSegment.Refresh}") ||
                    context.Request.Path.StartsWithSegments($"/{KrafterRoute.Tokens}") ||
                    context.Request.Path.StartsWithSegments($"/{KrafterRoute.ExternalAuth}/{RouteSegment.Google}") ||
                    context.Request.Path.StartsWithSegments($"/{KrafterRoute.Tokens}/{RouteSegment.Current}") ||
                    context.Request.Path.StartsWithSegments($"/{KrafterRoute.Tokens}/{RouteSegment.Logout}")
                   )
                {
                    context.Token = currentToken;
                    return;
                }

                if (!string.IsNullOrEmpty(currentToken) && IsTokenExpired(currentToken) &&
                    !string.IsNullOrWhiteSpace(refreshToken))
                {
                    ILogger<Program> logger =
                        context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    IApiService apiService = context.HttpContext.RequestServices.GetRequiredService<IApiService>();

                    logger.LogInformation("Token expired in OnMessageReceived, attempting refresh...");

                    try
                    {
                        var refreshRequest = new RefreshTokenRequest
                        {
                            Token = currentToken, RefreshToken = refreshToken
                        };
                        Response<TokenResponse> refreshResponse =
                            await apiService.RefreshTokenAsync(refreshRequest, CancellationToken.None);

                        if (refreshResponse is { Data: not null, IsError: false })
                        {
                            currentToken = refreshResponse.Data.Token;
                            if (refreshResponse.Data.Permissions is { Count: > 0 })
                            {
                                context.HttpContext.Items[StorageConstants.Local.Permissions] =
                                    refreshResponse.Data.Permissions;
                            }

                            context.HttpContext.Items[StorageConstants.Local.AuthToken] = refreshResponse.Data.Token;
                            context.HttpContext.Items[StorageConstants.Local.RefreshToken] =
                                refreshResponse.Data.RefreshToken;
                            context.HttpContext.Items[StorageConstants.Local.AuthTokenExpiryDate] =
                                refreshResponse.Data.TokenExpiryTime;
                            context.HttpContext.Items[StorageConstants.Local.RefreshTokenExpiryDate] =
                                refreshResponse.Data.RefreshTokenExpiryTime;

                            logger.LogInformation("Token refreshed successfully in OnMessageReceived");
                        }
                        else
                        {
                            logger.LogWarning("No refresh token available for proactive refresh");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error during proactive token refresh in OnMessageReceived");
                    }

                    context.Token = currentToken;
                }
                else
                {
                    context.Token = currentToken;
                }
            },
            OnTokenValidated = async context =>
            {
                if (context.Principal?.Identity is not ClaimsIdentity claimsIdentity)
                {
                    return;
                }

                if (context.HttpContext.Items.TryGetValue(StorageConstants.Local.Permissions,
                        out object? freshPermissions) &&
                    freshPermissions is List<string> permissionsFromTempHttpContextItem)
                {
                    foreach (string permission in permissionsFromTempHttpContextItem)
                    {
                        if (!string.IsNullOrWhiteSpace(permission) &&
                            !claimsIdentity.HasClaim(KrafterClaims.Permission, permission))
                        {
                            claimsIdentity.AddClaim(new Claim(KrafterClaims.Permission, permission));
                        }
                    }
                }
                else
                {
                    IKrafterLocalStorageService localStorage = context.HttpContext.RequestServices
                        .GetRequiredService<IKrafterLocalStorageService>();
                    ICollection<string>? permissionsFromMemoryCache = await localStorage.GetCachedPermissionsAsync();
                    if (permissionsFromMemoryCache == null || permissionsFromMemoryCache.Count == 0)
                    {
                        return;
                    }

                    foreach (string permission in permissionsFromMemoryCache)
                    {
                        if (!string.IsNullOrWhiteSpace(permission) &&
                            !claimsIdentity.HasClaim(KrafterClaims.Permission, permission))
                        {
                            claimsIdentity.AddClaim(new Claim(KrafterClaims.Permission, permission));
                        }
                    }
                }
            },
            OnChallenge = context =>
            {
                // For browser requests, redirect to the login page instead of returning 401
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    context.Response.Redirect("/login?ReturnUrl=" +
                                              Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
                }

                return Task.CompletedTask;
            }
        };
    });

string? apiUrl = builder.Configuration.GetValue<string>("services:krafter-api:https:0");
if (string.IsNullOrWhiteSpace(apiUrl))
{
    throw new Exception("API URL not found");
}

// Override RemoteHostUrl with Aspire service discovery URL for server-side Refit calls
builder.Configuration["RemoteHostUrl"] = apiUrl;

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IKrafterLocalStorageService, KrafterLocalStorageServiceServer>();
builder.Services.AddUIServices();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthenticationStateProvider>()
    .AddAuthorizationCore(RegisterPermissionClaimsClass.RegisterPermissionClaims);
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<TenantIdentifier>();

// Server uses apiUrl for both AditiKraft.Krafter.Backend and BFF since it manages cookies directly
builder.Services.AddKrafterRefitClients();
WebApplication app = builder.Build();
app.UseOutputCache();

app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(AditiKraft.Krafter.UI.Web.Client._Imports).Assembly);
app.MapGet("/cached", () => "Hello world!")
    .CacheOutput();
MapAuthTokenEndpoints(app);

app.Run();

static bool IsTokenExpired(string token)
{
    try
    {
        var handler = new JwtSecurityTokenHandler();
        if (handler.ReadToken(token) is JwtSecurityToken jwtToken)
        {
            // Add 1 minute buffer to avoid edge cases
            return jwtToken.ValidTo <= DateTime.UtcNow.AddMinutes(1);
        }

        return true;
    }
    catch
    {
        return true;
    }
}

static void MapAuthTokenEndpoints(WebApplication app)
{
    app.MapGet($"/{KrafterRoute.Tokens}/{RouteSegment.Current}", async (IApiService apiService) =>
    {
        Response<TokenResponse> res = await apiService.GetCurrentTokenAsync(CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    }).RequireAuthorization();

    app.MapPost($"/{KrafterRoute.Tokens}", async ([FromBody] TokenRequest request, IApiService apiService,
        [FromServices] IHttpClientFactory clientFactory) =>
    {
        Response<TokenResponse> res = await apiService.CreateTokenAsync(request, CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    });
    app.MapPost($"/{KrafterRoute.Tokens}/{RouteSegment.Refresh}", async ([FromBody] RefreshTokenRequest request,
        IApiService apiService,
        [FromServices] IHttpClientFactory clientFactory) =>
    {
        Response<TokenResponse> tokenResponse = await apiService.RefreshTokenAsync(request, CancellationToken.None);
        return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
    });

    app.MapPost($"/{KrafterRoute.ExternalAuth}/{RouteSegment.Google}",
        async ([FromBody] TokenRequest request, IApiService apiService) =>
        {
            Response<TokenResponse> tokenResponse = await apiService.ExternalAuthAsync(request, CancellationToken.None);
            return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
        });

    app.MapPost($"/{KrafterRoute.Tokens}/{RouteSegment.Logout}", async (IApiService apiService) =>
    {
        await apiService.LogoutAsync(CancellationToken.None);
        return Results.Ok();
    });
}
