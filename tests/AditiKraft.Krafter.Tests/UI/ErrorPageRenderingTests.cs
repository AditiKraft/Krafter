using System.Net;
using System.Security.Claims;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;
using AditiKraft.Krafter.UI.Web.Components;
using AditiKraft.Krafter.UI.Web.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Radzen;

namespace AditiKraft.Krafter.Tests.UI;

public sealed class ErrorPageRenderingTests
{
    [Fact]
    public async Task ErrorRouteRemainsStaticAndProvidesRealRecoveryLinks()
    {
        await using var host = await ErrorHost.StartAsync();
        using HttpResponseMessage response = await host.Client.GetAsync("/Error");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Something went wrong", html);
        Assert.Contains("Request ID:", html);
        Assert.Contains("href=\"/\" data-enhance-nav=\"false\"", html);
        Assert.Contains($"href=\"{new Uri(host.Client.BaseAddress!, "Error").AbsoluteUri}\" data-enhance-nav=\"false\"", html);
        Assert.DoesNotContain("<button", html);
        Assert.DoesNotContain("<!--Blazor:", html);
        Assert.DoesNotContain("_framework/blazor.web.js", html);
        Assert.DoesNotContain("webassembly-loading-spinner", html);
    }

    [Fact]
    public async Task ExceptionReexecutionRetriesTheOriginalPathAndKeepsDetailsPrivate()
    {
        await using var host = await ErrorHost.StartAsync();
        using HttpResponseMessage response = await host.Client.GetAsync("/ui-test/failure?filter=active&sort=name");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Something went wrong", html);
        Assert.Contains("Request ID:", html);
        Assert.Contains($"href=\"{new Uri(host.Client.BaseAddress!, "ui-test/failure").AbsoluteUri}?filter=active&amp;sort=name\"", html);
        Assert.DoesNotContain("private exception sentinel", html);
        Assert.DoesNotContain("<!--Blazor:", html);
        Assert.DoesNotContain("_framework/blazor.web.js", html);
    }

    private sealed class ErrorHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<ErrorHost> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ApplicationName = typeof(App).Assembly.FullName
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
            builder.Services.AddRadzenComponents();
            builder.Services.AddScoped<ThemeManager>();
            builder.Services.AddAuthorization();
            builder.Services.AddCascadingAuthenticationState();
            var authenticationState = new AnonymousAuthenticationState();
            builder.Services.AddSingleton<AuthenticationStateProvider>(authenticationState);
            builder.Services.AddSingleton<IAuthenticationService, NoAuthentication>();
            // Server rendering never opens realtime connections. Keep this fixture free
            // of backend APIs, database state, and browser credential storage.
            builder.Services.AddSingleton(new SignalRService(null!, null!, new FormFactorServer(),
                authenticationState, NullLogger<SignalRService>.Instance));
            WebApplication app = builder.Build();
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseAntiforgery();
            app.MapGet("/ui-test/failure", () => Task.FromException(new InvalidOperationException("private exception sentinel")));
            app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(AditiKraft.Krafter.UI.Web.Client._Imports).Assembly);
            try
            {
                await app.StartAsync();
                return new ErrorHost(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) });
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class AnonymousAuthenticationState : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class NoAuthentication : IAuthenticationService
    {
        public TwoFactorChallengeResponse? PendingChallenge => null;
        public string? ErrorMessage => null;
        public event Action<string?>? LoginChange { add { } remove { } }
        public Task<bool> LoginAsync(TokenRequest model) => throw new NotSupportedException();
        public Task<bool> LogoutAsync(string methodName, long? expectedSessionVersion = null) => throw new NotSupportedException();
        public Task<bool> VerifyTwoFactorAsync(string code, bool recovery) => throw new NotSupportedException();
        public Task<bool> RefreshAsync() => throw new NotSupportedException();
        public void CancelTwoFactor() => throw new NotSupportedException();
    }
}
