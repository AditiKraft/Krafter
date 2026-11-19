using System.Net.Http.Json;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorEndpointTests
{
    [Fact]
    public async Task DiscoveredOperationsKeepTheirRoutesPoliciesAndHandlerRegistrationAsync()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        WebApplicationBuilder builder = Builder();
        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddValidatorsFromAssemblyContaining<TwoFactorRequestValidator>();
        builder.Services.AddApplicationServices().AddRouteDiscovery();
        builder.Services.AddScoped(_ => scope.Mfa);
        builder.Services.AddScoped(_ => scope.Sessions);
        builder.Services.AddScoped<IEmailService, CapturingEmail>();
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("authentication", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromMinutes(1);
        }));
        Type[] operations = [typeof(CompleteTwoFactorLogin), typeof(GetTwoFactorStatus),
            typeof(BeginTwoFactorEnrollment), typeof(RequestEnrollmentEmail), typeof(ConfirmTwoFactorEnrollment), typeof(DisableTwoFactor),
            typeof(RegenerateRecoveryCodes), typeof(ValidateSession)];
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        RouteGroupBuilder api = app.MapGroup("/api");
        foreach (IRouteRegistrar route in app.Services.GetServices<IRouteRegistrar>()
                     .Where(route => operations.Contains(route.GetType().DeclaringType)))
        {
            route.MapRoute(api);
        }
        using (IServiceScope services = app.Services.CreateScope())
        {
            foreach (Type operation in operations)
            {
                Type handler = operation.GetNestedType("Handler", System.Reflection.BindingFlags.NonPublic)!;
                Assert.NotNull(services.ServiceProvider.GetRequiredService(handler));
            }
        }
        await app.StartAsync();
        try
        {
            RouteEndpoint[] endpoints = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
            string[] paths = ["/api/two-factor/login", "/api/two-factor/status", "/api/two-factor/setup",
                "/api/two-factor/enrollment-email", "/api/two-factor/confirm", "/api/two-factor/disable", "/api/two-factor/recovery-codes", "/api/tokens/validate"];
            foreach (string path in paths)
            {
                RouteEndpoint endpoint = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == path);
                if (path.EndsWith("/login", StringComparison.Ordinal))
                {
                    Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
                }
                else
                {
                    Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
                }
                if (path.Contains("two-factor", StringComparison.Ordinal))
                {
                    Assert.Equal("authentication", endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
                }
            }
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using HttpResponseMessage forbidden = await client.PostAsJsonAsync("/api/two-factor/login",
                new TwoFactorRequest { ChallengeId = "unknown", Code = "123456" });
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.True(forbidden.Headers.CacheControl?.NoStore);
            client.DefaultRequestHeaders.Add("X-Krafter-Mfa", "1");
            using HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/two-factor/login",
                new TwoFactorRequest { ChallengeId = "unknown", Code = "123456" });
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
            Assert.True(invalid.Headers.CacheControl?.NoStore);
            using HttpResponseMessage malformed = await client.PostAsJsonAsync("/api/two-factor/login", new TwoFactorRequest());
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task EnrollmentEmailDeliversTheCodeSeparatelyFromItsBrowserHandle()
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = await scope.AddUserAsync();
        Assert.True((await scope.Users.RemovePasswordAsync(user)).Succeeded);
        var login = await scope.GoogleLoginAsync(user.Id);
        var principal = MfaHarness.Principal(login.Data!.Tokens!);
        DateTimeOffset signIn = DateTimeOffset.FromUnixTimeSeconds(harness.Clock.GetUtcNow().ToUnixTimeSeconds());
        harness.Clock.Advance(TimeSpan.FromMinutes(4));
        var email = new CapturingEmail();
        var handler = new RequestEnrollmentEmail.Handler(scope.Mfa, email);
        var result = await handler.SendAsync(user.Id, principal, default);
        Assert.False(result.IsError, result.Message);
        Assert.Equal(signIn.AddMinutes(5), result.Data!.ExpiresAt);
        Assert.Contains(result.Data.ExpiresAt.UtcDateTime.ToString("u", System.Globalization.CultureInfo.InvariantCulture), email.Body);
        Assert.Equal(429, (await handler.SendAsync(user.Id, principal, default)).StatusCode);
        Assert.Equal(user.Email, email.To);
        Assert.DoesNotContain(result.Data!.Id, email.Body);
        string code = System.Text.RegularExpressions.Regex.Match(email.Body, @"<strong>([0-9]{8})</strong>").Groups[1].Value;
        Assert.Equal(8, code.Length);
        var proof = new TwoFactorManagementRequest { EnrollmentEmailId = result.Data.Id, Code = code };
        Assert.False((await scope.Mfa.BeginEnrollmentAsync(user.Id, proof, principal)).IsError);
    }

    private sealed class CapturingEmail : IEmailService
    {
        public string To { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;
        public Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken)
        {
            To = email;
            Body = htmlMessage;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("/api/two-factor/status", false)]
    [InlineData("/api/tokens", true)]
    [InlineData("/api/external-auth/google", true)]
    [InlineData("/api/tokens/refresh", true)]
    public async Task AbortedBffRequestCancelsItsOutgoingRefitRequestAsync(string path, bool post)
    {
        using var transport = new WaitingTransport();
        using var backend = new HttpClient(transport) { BaseAddress = new Uri("https://api.example.test") };
        WebApplicationBuilder builder = Builder();
        builder.Services.AddSingleton(RestService.For<ITwoFactorApi>(backend));
        builder.Services.AddSingleton(RestService.For<IAuthApi>(backend));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHybridCache();
        builder.Services.AddScoped<IAuthStorageService, AuthStorageServiceServer>();
        builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTwoFactorBffEndpoints();
        app.MapAuthTokenBffEndpoints();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var cancellation = new CancellationTokenSource();
            Task<HttpResponseMessage> request = post
                ? client.PostAsJsonAsync(path, new { Email = "user@example.test", Password = "unused", Code = "unused",
                    Token = "unused", RefreshToken = "unused" }, cancellation.Token)
                : client.GetAsync(path, cancellation.Token);
            await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            await transport.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplicationBuilder Builder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
        builder.Services.AddAuthorization();
        return builder;
    }

    private sealed class WaitingTransport : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The request must be canceled.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Canceled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user")], Scheme.Name)), Scheme.Name)));
    }
}
