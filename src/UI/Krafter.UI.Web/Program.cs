using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Infrastructure.Api;
using Krafter.UI.Web.Client.Infrastructure.Http;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Krafter.UI.Web.Client.Infrastructure.Storage;
using Krafter.UI.Web.Client.Kiota;
using Krafter.UI.Web.Components;
using Krafter.UI.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Radzen;


var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpContextAccessor();
builder.AddRedisDistributedCache(connectionName: "cache");
builder.AddRedisOutputCache(connectionName: "cache");
builder.Services.AddHybridCache();
builder.Services.AddScoped<ServerAuthenticationHandler>();
builder.Services.AddSingleton<IFormFactor, FormFactorServer>();
builder.Services.AddScoped<IApiService, ServerSideApiService>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
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
                var currentToken = context.Request.Cookies[StorageConstants.Local.AuthToken];
                var refreshToken = context.Request.Cookies[StorageConstants.Local.RefreshToken];

                // skip refresh logic for api endpoints we need this only in the case of prerendered Blazor components
                if (context.Request.Path.StartsWithSegments("/tokens/refresh") || 
                    context.Request.Path.StartsWithSegments("/tokens/create") ||
                    context.Request.Path.StartsWithSegments("/external-auth/google") ||
                    context.Request.Path.StartsWithSegments("/tokens/current") ||
                    context.Request.Path.StartsWithSegments("/tokens/logout")
                    )
                {
                    context.Token = currentToken;
                    return;
                }
                if ((!string.IsNullOrEmpty(currentToken) && (IsTokenExpired(currentToken))) && !string.IsNullOrWhiteSpace(refreshToken))
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    var apiService = context.HttpContext.RequestServices.GetRequiredService<IApiService>();

                    logger.LogInformation("Token expired in OnMessageReceived, attempting refresh...");

                    try
                    {
                       
                        var refreshRequest = new RefreshTokenRequest()
                        {
                            
                            Token = currentToken,
                            RefreshToken = refreshToken
                        };
                        var refreshResponse = await apiService.RefreshTokenAsync(refreshRequest, CancellationToken.None);

                        if (refreshResponse is { Data: not null, IsError: false })
                        {
                            currentToken = refreshResponse.Data.Token;
                            if (refreshResponse.Data.Permissions is { Count: > 0 })
                            {
                                context.HttpContext.Items[StorageConstants.Local.Permissions] = refreshResponse.Data.Permissions;
                            }
                            context.HttpContext.Items[StorageConstants.Local.AuthToken] = refreshResponse.Data.Token;
                            context.HttpContext.Items[StorageConstants.Local.RefreshToken] = refreshResponse.Data.RefreshToken;
                            context.HttpContext.Items[StorageConstants.Local.AuthTokenExpiryDate] = refreshResponse.Data.TokenExpiryTime;
                            context.HttpContext.Items[StorageConstants.Local.RefreshTokenExpiryDate] = refreshResponse.Data.RefreshTokenExpiryTime;

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
                    return;

                if (context.HttpContext.Items.TryGetValue(StorageConstants.Local.Permissions, out var freshPermissions) && freshPermissions is List<string> permissionsFromTempHttpContextItem)
                {
                    foreach (var permission in permissionsFromTempHttpContextItem)
                    {
                        if (!string.IsNullOrWhiteSpace(permission) && !claimsIdentity.HasClaim(KrafterClaims.Permission, permission))
                        {
                            claimsIdentity.AddClaim(new Claim(KrafterClaims.Permission, permission));
                        }
                    }
                }
                else  
                {
                    var localStorage = context.HttpContext.RequestServices.GetRequiredService<IKrafterLocalStorageService>();
                    var permissionsFromMemoryCache = await localStorage.GetCachedPermissionsAsync();
                    if (permissionsFromMemoryCache == null || permissionsFromMemoryCache.Count == 0)
                        return;
                    
                    foreach (var permission in permissionsFromMemoryCache)
                    {
                        if (!string.IsNullOrWhiteSpace(permission) && !claimsIdentity.HasClaim(KrafterClaims.Permission, permission))
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
                    context.Response.Redirect("/login?ReturnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
                }

                return Task.CompletedTask;
            }
        };
    });

var apiUrl = builder.Configuration.GetValue<string>("services:krafter-api:https:0");
if (string.IsNullOrWhiteSpace(apiUrl))
{
    throw new Exception("API URL not found");
}

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IKrafterLocalStorageService, KrafterLocalStorageServiceServer>();
builder.Services.AddUIServices(apiUrl);
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthenticationStateProvider>()
      .AddAuthorizationCore(RegisterPermissionClaimsClass.RegisterPermissionClaims);
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<TenantIdentifier>();

builder.Services.AddHttpClient("KrafterUIAPI", client =>
{
    HttpClientTenantConfigurator.SetAPITenantHttpClientDefaults(builder.Services, client);
})
   .AddHttpMessageHandler<ServerAuthenticationHandler>()
    .Services
    .AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("KrafterUIAPI"));
builder.Services.AddKrafterKiotaClient(builder.Configuration["RemoteHostUrl"]);
var app = builder.Build();
app.UseOutputCache();

app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
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
        
        typeof(Krafter.UI.Web.Client._Imports).Assembly);
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
    app.MapGet("/tokens/current", async (IApiService apiService) =>
    {
        var res = await apiService.GetCurrentTokenAsync(CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    }).RequireAuthorization();

    app.MapPost("/tokens/create", async ([FromBody] TokenRequestInput request,  IApiService apiService,  [FromServices] IHttpClientFactory clientFactory) =>
    {
        var res = await apiService.CreateTokenAsync(request, CancellationToken.None);
        return Results.Json(res, statusCode: res.StatusCode);
    });
    app.MapPost("/tokens/refresh", async ([FromBody] RefreshTokenRequest request, IApiService apiService,   [FromServices] IHttpClientFactory clientFactory) =>
    {
        var tokenResponse = await apiService.RefreshTokenAsync(request, CancellationToken.None);
        return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
    });

    app.MapPost("/external-auth/google", async ([FromBody] TokenRequestInput request, IApiService apiService) =>
    {
        var tokenResponse = await apiService.ExternalAuthAsync(request, CancellationToken.None);
        return Results.Json(tokenResponse, statusCode: tokenResponse.StatusCode);
    });

    app.MapPost("/tokens/logout", async ( IApiService apiService) =>
    {
        await apiService.LogoutAsync(CancellationToken.None);
        return Results.Ok();
    });
}