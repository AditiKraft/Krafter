using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Refit;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorBlazorSessionTests
{
    [Theory]
    [InlineData(BlazorHostingMode.SplitHost, 0, false)]
    [InlineData(BlazorHostingMode.SingleHost, 0, false)]
    [InlineData(BlazorHostingMode.SplitHost, 1, false)]
    [InlineData(BlazorHostingMode.SingleHost, 1, false)]
    [InlineData(BlazorHostingMode.SplitHost, 1, true)]
    [InlineData(BlazorHostingMode.SingleHost, 1, true)]
    [InlineData(BlazorHostingMode.SplitHost, 2, false)]
    [InlineData(BlazorHostingMode.SingleHost, 2, false)]
    public async Task CachedPermissionsMustBelongToTheExactVerifiedToken(BlazorHostingMode mode, int cookieKind, bool fromItems)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        TokenResponse verified = (await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).Data!.Tokens!;
        string otherCookie = TokenTestData.Create("other-cookie-user").Token;
        using var client = new HttpClient(new SessionTransport(verified.Token)) { BaseAddress = new Uri("https://api.example.com") };
        var accessor = new HttpContextAccessor();
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddHybridCache();
        registrations.AddSingleton<IHttpContextAccessor>(accessor);
        registrations.AddSingleton<IAuthStorageService, AuthStorageServiceServer>();
        registrations.AddSingleton<IAuthApi>(RestService.For<IAuthApi>(client));
        using ServiceProvider services = registrations.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        accessor.HttpContext = http;
        http.Request.Path = "/api/RealtimeHub";
        http.Request.QueryString = new QueryString("?access_token=" + verified.Token);
        string cookie = $"{StorageConstants.Local.Permissions}=Permissions.Users.Delete";
        if (cookieKind != 0)
            cookie += $"; {StorageConstants.Local.AuthToken}={(cookieKind == 2 ? verified.Token : otherCookie)}";
        http.Request.Headers.Cookie = cookie;
        HybridCache cache = services.GetRequiredService<HybridCache>();
        await cache.SetAsync($"{StorageConstants.Local.Permissions}_{user.Id}", new List<string> { "Permissions.Users.View" });
        await cache.SetAsync($"{StorageConstants.Local.Permissions}_other-cookie-user", new List<string> { "Permissions.Users.Delete" });
        if (fromItems) http.Items[StorageConstants.Local.Permissions] = new List<string> { "Permissions.Users.Delete" };
        TokenValidatedContext context = Context(http, verified);
        await new BlazorJwtBearerEvents(mode, c => scope.Sessions.ValidateAsync(c.Principal!)).TokenValidated(context);
        Assert.Null(context.Result?.Failure);
        Assert.False(context.Principal!.HasClaim(AppClaimTypes.Permission, "Permissions.Users.Delete"));
        Assert.Equal(cookieKind == 2, context.Principal.HasClaim(AppClaimTypes.Permission, "Permissions.Users.View"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{invalid")]
    [InlineData("timeout")]
    [InlineData("resilience-timeout")]
    [InlineData("open-circuit")]
    public async Task UnavailableOrMalformedSessionValidationFailsAuthenticationWithoutThrowing(string scenario)
    {
        TokenResponse token = TokenTestData.Create();
        using var client = new HttpClient(new InvalidResponseTransport(scenario)) { BaseAddress = new Uri("https://api.example.com") };
        using ServiceProvider services = new ServiceCollection().AddSingleton<IAuthApi>(RestService.For<IAuthApi>(client)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        TokenValidatedContext context = Context(http, token);
        await new BlazorJwtBearerEvents(BlazorHostingMode.SplitHost).TokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{invalid")]
    [InlineData("{\"isError\":false,\"statusCode\":200,\"data\":null}")]
    public async Task InvalidSuccessfulAuthEnvelopesProduceStructuredErrors(string body)
    {
        using var client = new HttpClient(new InvalidResponseTransport(body)) { BaseAddress = new Uri("https://api.example.com") };
        IAuthApi api = RestService.For<IAuthApi>(client);
        Response<LoginResponse> result = await AuthApiErrors.CallAsync(() => api.CreateTokenAsync(new()));
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Equal(502, result.StatusCode);
    }

    [Theory]
    [InlineData("null", 502)]
    [InlineData("{invalid", 502)]
    [InlineData("timeout", 503)]
    [InlineData("http-error", 503)]
    public async Task ManagementResponseFailuresProduceFeedback(string body, int status)
    {
        using var client = new HttpClient(new InvalidResponseTransport(body)) { BaseAddress = new Uri("https://api.example.com") };
        ITwoFactorApi api = RestService.For<ITwoFactorApi>(client);
        Response result = await AuthApiErrors.CallAsync(() => api.DisableAsync(new()));
        Assert.True(result.IsError);
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task SuccessfulManagementResponsesDoNotRequireADataField()
    {
        using var client = new HttpClient(new InvalidResponseTransport("{}")) { BaseAddress = new Uri("https://api.example.com") };
        ITwoFactorApi api = RestService.For<ITwoFactorApi>(client);
        Response result = await AuthApiErrors.CallAsync(() => api.DisableAsync(new()));
        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData("primary", "resilience-timeout")]
    [InlineData("primary", "open-circuit")]
    [InlineData("mfa", "resilience-timeout")]
    [InlineData("mfa", "open-circuit")]
    [InlineData("google", "resilience-timeout")]
    [InlineData("google", "open-circuit")]
    public async Task ServerLoginFlowsNormalizeResilienceRejections(string flow, string scenario)
    {
        using var client = new HttpClient(new InvalidResponseTransport(scenario)) { BaseAddress = new Uri("https://api.example.com") };
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddHybridCache();
        using ServiceProvider services = registrations.BuildServiceProvider();
        var server = new ServerAuthApiService(RestService.For<IAuthApi>(client), new EmptyStorage(),
            services.GetRequiredService<HybridCache>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ServerAuthApiService>.Instance);
        int status;
        if (flow == "mfa")
        {
            Response<TokenResponse> response = await server.CompleteTwoFactorAsync(new(), CancellationToken.None);
            Assert.True(response.IsError); status = response.StatusCode;
        }
        else
        {
            Response<LoginResponse> response = flow == "google"
                ? await server.ExternalAuthAsync(new(), CancellationToken.None)
                : await server.CreateTokenAsync(new(), CancellationToken.None);
            Assert.True(response.IsError); status = response.StatusCode;
        }
        Assert.Equal(503, status);
    }

    [Theory]
    [InlineData("setup", "null", 502)]
    [InlineData("setup", "{}", 502)]
    [InlineData("setup", "{invalid", 502)]
    [InlineData("setup", "timeout", 503)]
    [InlineData("setup", "http-error", 503)]
    [InlineData("setup", "resilience-timeout", 503)]
    [InlineData("setup", "open-circuit", 503)]
    [InlineData("disable", "null", 502)]
    [InlineData("disable", "open-circuit", 503)]
    [InlineData("disable", "{}", 200)]
    public async Task ManagementBffNormalizesDependencyFailures(string operation, string scenario, int expectedStatus)
    {
        using var client = new HttpClient(new InvalidResponseTransport(scenario)) { BaseAddress = new Uri("https://api.example.com") };
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Services.AddHybridCache();
        builder.Services.AddSingleton<IAuthApi>(RestService.For<IAuthApi>(client));
        builder.Services.AddSingleton<ITwoFactorApi>(RestService.For<ITwoFactorApi>(client));
        builder.Services.AddSingleton<IAuthStorageService>(new EmptyStorage());
        builder.Services.AddSingleton<IAuthApiService, ServerAuthApiService>();
        await using WebApplication app = builder.Build();
        app.MapTwoFactorBffEndpoints();
        Endpoint endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>().Single(e => e.RoutePattern.RawText == "/api/two-factor/" + operation);
        var http = new DefaultHttpContext { RequestServices = app.Services };
        http.Request.Method = "POST";
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("app.example.com");
        http.Request.Path = "/api/two-factor/" + operation;
        http.Request.Headers["X-Krafter-Mfa"] = "1";
        http.Request.ContentType = "application/json";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        http.Request.ContentLength = 2;
        http.Features.Set<IHttpRequestBodyDetectionFeature>(new JsonBodyFeature());
        http.Response.Body = new MemoryStream();
        await endpoint.RequestDelegate!(http);
        Assert.Equal(expectedStatus, http.Response.StatusCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SplitHostValidatesTheExactVerifiedTokenEvenForQueryOnlyOrConflictingCookies(bool conflictingCookie, bool legacyToken)
    {
        TokenResponse token = TokenTestData.Create();
        var transport = new SessionTransport(token.Token);
        using var client = new HttpClient(transport) { BaseAddress = new Uri("https://api.example.com") };
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IAuthApi>(RestService.For<IAuthApi>(client))
            .AddSingleton<IAuthStorageService>(new EmptyStorage(conflictingCookie ? "another-cookie-token" : null))
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Path = "/api/RealtimeHub";
        http.Request.QueryString = new QueryString("?access_token=" + token.Token);
        TokenValidatedContext context = Context(http, token);
        context.SecurityToken = legacyToken
            ? new JwtSecurityTokenHandler().ReadJwtToken(token.Token)
            : new JsonWebToken(token.Token);
        await new BlazorJwtBearerEvents(BlazorHostingMode.SplitHost).TokenValidated(context);
        Assert.Null(context.Result?.Failure);
        Assert.Equal("Bearer " + token.Token, transport.Authorization);
    }

    [Fact]
    public async Task SingleHostValidatesTheResolvedTenantInProcessAndRejectsRevokedSessions()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope("blue");
        var user = await scope.AddUserAsync();
        var login = await scope.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        TokenResponse token = login.Data!.Tokens!;
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IAuthStorageService>(new EmptyStorage()).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Host = new HostString("internal-app");
        http.Request.Headers["x-tenant-identifier"] = "blue";
        http.Request.Path = "/api/users";
        var events = new BlazorJwtBearerEvents(BlazorHostingMode.SingleHost,
            context => scope.Sessions.ValidateAsync(context.Principal!));
        TokenValidatedContext valid = Context(http, token);
        await events.TokenValidated(valid);
        Assert.Null(valid.Result?.Failure);
        user.AuthenticationVersion++;
        Assert.True((await scope.Users.UpdateAsync(user)).Succeeded);
        TokenValidatedContext revoked = Context(http, token);
        await events.TokenValidated(revoked);
        Assert.NotNull(revoked.Result?.Failure);
    }

    private static TokenValidatedContext Context(HttpContext http, TokenResponse token) => new(http,
        new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), new JwtBearerOptions())
    { Principal = MfaHarness.Principal(token), SecurityToken = new JsonWebToken(token.Token) };

    private sealed class SessionTransport(string expectedToken) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            bool accepted = Authorization == "Bearer " + expectedToken;
            return Task.FromResult(new HttpResponseMessage(accepted ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(JsonSerializer.Serialize(accepted
                    ? Response<bool>.Success(true) : Response<bool>.Unauthorized("Invalid session.")), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class InvalidResponseTransport(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => body switch
        {
            "timeout" => Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated timeout.")),
            "http-error" => Task.FromException<HttpResponseMessage>(new HttpRequestException("Simulated unavailable service.")),
            "resilience-timeout" => Task.FromException<HttpResponseMessage>(new Polly.Timeout.TimeoutRejectedException("Simulated timeout.")),
            "open-circuit" => Task.FromException<HttpResponseMessage>(new Polly.CircuitBreaker.BrokenCircuitException("Simulated open circuit.")),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { RequestMessage = request, Content = new StringContent(body, Encoding.UTF8, "application/json") })
        };
    }

    private sealed class JsonBodyFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class EmptyStorage(string? token = null) : IAuthStorageService
    {
        public Task ClearCacheAsync() => Task.CompletedTask;
        public ValueTask CacheAuthTokens(TokenResponse response) => ValueTask.CompletedTask;
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => ValueTask.FromResult(default(DateTime));
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => ValueTask.FromResult(default(DateTime));
        public ValueTask<string?> GetCachedAuthTokenAsync() => ValueTask.FromResult(token);
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult<string?>(null);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => ValueTask.FromResult<ICollection<string>?>(null);
    }
}
