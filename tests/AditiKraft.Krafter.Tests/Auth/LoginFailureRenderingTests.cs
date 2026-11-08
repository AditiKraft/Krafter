using System.Security.Claims;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class LoginFailureRenderingTests
{
    [Theory]
    [InlineData(false, "login-with-google-dark.svg")]
    [InlineData(true, "login-with-google-light.svg")]
    public async Task GoogleSignInRendersNamedButtonWithDecorativeThemeAsset(bool darkTheme, string asset)
    {
        await using var page = new LoginPage("", "/users", authenticated: false, darkTheme: darkTheme);
        await page.RenderAsync();
        await page.RunAsync(() =>
        {
            Assert.Equal(ButtonType.Button, page.GoogleButton.ButtonType);
            Assert.True(page.GoogleButton.Click.HasDelegate);
            Assert.False(page.GoogleButton.Disabled);
            Assert.Equal("Sign in with Google", page.GoogleButton.Attributes["aria-label"]);
            Assert.Equal(asset, page.GoogleImage.Path);
            Assert.Equal("", page.GoogleImage.AlternateText);
            Assert.False(page.GoogleImage.Click.HasDelegate);

            // Inspect actual Razor/Radzen output; browser Tab/Enter/Space validation is separate.
            string html = page.Root.ToHtmlString();
            int label = html.IndexOf("aria-label=\"Sign in with Google\"", StringComparison.Ordinal);
            Assert.True(label >= 0);
            int opening = html.LastIndexOf("<button", label, StringComparison.Ordinal);
            int closing = html.IndexOf("</button>", label, StringComparison.Ordinal);
            Assert.True(opening >= 0 && closing > label);
            string buttonHtml = html[opening..closing];
            Assert.Contains("type=\"button\"", buttonHtml);
            Assert.Contains($"src=\"{asset}\"", buttonHtml);
            Assert.Contains("alt=\"\"", buttonHtml);
            Assert.Contains("aria-hidden=\"true\"", buttonHtml);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("google-auth-failed", "Unable to sign in with Google. Please try again.")]
    [InlineData("invalid-google-state", "Google sign-in expired or could not be verified. Please try again.")]
    public async Task KnownOAuthErrorsRemainVisibleEvenWithAnExistingSession(string error, string message)
    {
        await using var page = new LoginPage(error, "/account/two-factor?tab=codes#help", authenticated: true);
        await page.RenderAsync();

        await page.RunAsync(async () =>
        {
            // Static rendering omits this callback. Exercise the actual redirect decision as hydration does.
            await ((IHandleAfterRender)page.Component).OnAfterRenderAsync();
            Assert.Contains("role=\"alert\"", page.Root.ToHtmlString());
            Assert.Contains(message, page.Root.ToHtmlString());
            Assert.Null(page.Navigation.Target);
        });
    }

    [Theory]
    [InlineData("private-provider-response")]
    [InlineData("<script>private-provider-response</script>")]
    [InlineData("google-auth-failed:private-provider-response")]
    public async Task UnknownErrorQueryTextIsNeverDisplayed(string error)
    {
        await using var page = new LoginPage(error, "/users", authenticated: false);
        await page.RenderAsync();
        await page.RunAsync(() =>
        {
            string visibleText = System.Text.RegularExpressions.Regex.Replace(page.Root.ToHtmlString(), "<[^>]*>", "");
            Assert.DoesNotContain("private-provider-response", visibleText);
            Assert.DoesNotContain("role=\"alert\"", page.Root.ToHtmlString());
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("/account/two-factor?tab=codes#help", "/account/two-factor?tab=codes#help")]
    [InlineData("//outside.example.test", "/")]
    [InlineData("/\t/outside.example.test", "/")]
    public async Task RetryingGoogleKeepsTheTenantAndOnlyASafeReturnTarget(string returnUrl, string expected)
    {
        await using var page = new LoginPage("google-auth-failed", returnUrl, authenticated: false);
        await page.RenderAsync();
        await page.RunAsync(() => page.GoogleButton.Click.InvokeAsync(new MouseEventArgs()));

        var start = new Uri(page.Navigation.Target!);
        Assert.Equal("app.example.test", start.Host);
        Assert.Equal("/authentication/google/start", start.AbsolutePath);
        var query = QueryHelpers.ParseQuery(start.Query);
        Assert.Equal("https://blue.example.test", query["origin"].ToString());
        Assert.Equal(expected, query["returnUrl"].ToString());
        Assert.True(page.Navigation.ForceLoad);
    }

    [Fact]
    public async Task APasswordRetryClearsTheOldGoogleErrorAndPreservesItsLocalDestination()
    {
        await using var page = new LoginPage("google-auth-failed", "/account/two-factor?tab=codes#help", authenticated: false);
        await page.RenderAsync();
        await page.RunAsync(() => page.Form.Submit.InvokeAsync(page.Form.Data));

        Assert.Equal("/account/two-factor?tab=codes#help", page.Navigation.Target);
        await page.RunAsync(() =>
        {
            Assert.DoesNotContain("Unable to sign in with Google", page.Root.ToHtmlString());
            return Task.CompletedTask;
        });
    }

    private sealed class LoginPage : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly HtmlRenderer renderer;
        private readonly Components components;
        private readonly bool authenticated;
        public Navigation Navigation { get; }
        public HtmlRootComponent Root { get; private set; }

        public LoginPage(string error, string returnUrl, bool authenticated, bool darkTheme = false)
        {
            this.authenticated = authenticated;
            Navigation = new Navigation(QueryHelpers.AddQueryString("https://blue.example.test/login",
                new Dictionary<string, string?> { ["error"] = error, ["ReturnUrl"] = returnUrl }));
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddSingleton<NavigationManager>(Navigation);
            registrations.AddSingleton<IJSRuntime, NoJavaScript>();
            registrations.AddSingleton<IAuthenticationService, CompletedAuthentication>();
            registrations.AddSingleton<ThemeService>();
            registrations.AddSingleton<ThemeManager>();
            registrations.AddBlazoredSessionStorage();
            registrations.AddSingleton(new AppUrls { RootUiUrl = "https://app.example.test", TenantBaseDomain = "example.test" });
            registrations.AddSingleton<Components>();
            registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<Components>());
            services = registrations.BuildServiceProvider();
            services.GetRequiredService<ThemeManager>().CurrentActive = darkTheme
                ? ThemeManager.ThemePreference.Dark : ThemeManager.ThemePreference.Light;
            components = services.GetRequiredService<Components>();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public Login Component => components.All.OfType<Login>().Single();
        public RadzenButton GoogleButton => components.All.OfType<RadzenButton>().Single(button =>
            button.Attributes?.TryGetValue("aria-label", out object? label) == true && Equals(label, "Sign in with Google"));
        public RadzenImage GoogleImage => components.All.OfType<RadzenImage>().Single();
        public RadzenTemplateForm<TokenRequest> Form => components.All.OfType<RadzenTemplateForm<TokenRequest>>().Single();
        public Task RunAsync(Func<Task> action) => renderer.Dispatcher.InvokeAsync(action);

        public Task RenderAsync() => RunAsync(async () =>
        {
            var identity = authenticated ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "alice")], "test") : new ClaimsIdentity();
            RenderFragment content = builder => { builder.OpenComponent<Login>(0); builder.CloseComponent(); };
            Root = await renderer.RenderComponentAsync<CascadingValue<Task<AuthenticationState>>>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(CascadingValue<Task<AuthenticationState>>.Value)] = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))),
                    [nameof(CascadingValue<Task<AuthenticationState>>.ChildContent)] = content
                }));
        });

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await services.DisposeAsync();
        }
    }

    private sealed class Components(IServiceProvider services) : IComponentActivator
    {
        public List<IComponent> All { get; } = [];
        public IComponent CreateInstance(Type type)
        {
            IComponent component;
            if (type == typeof(Login))
            {
                NavigationManager navigation = services.GetRequiredService<NavigationManager>();
                var login = new Login(services.GetRequiredService<IAuthenticationService>(), null!, navigation,
                    services.GetRequiredService<ThemeManager>(), services.GetRequiredService<AppUrls>(),
                    services.GetRequiredService<ISessionStorageService>());
                // The fixture supplies decoded routable parameters; rendering and user handlers are production code.
                var query = QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query);
                typeof(Login).GetProperty(nameof(Login.Error))!.SetValue(login, query["error"].ToString());
                typeof(Login).GetProperty(nameof(Login.ReturnUrl))!.SetValue(login, query["ReturnUrl"].ToString());
                component = login;
            }
            else component = (IComponent)ActivatorUtilities.CreateInstance(services, type);
            All.Add(component);
            return component;
        }
    }

    private sealed class Navigation : NavigationManager
    {
        public string? Target { get; private set; }
        public bool ForceLoad { get; private set; }
        public Navigation(string uri) => Initialize("https://blue.example.test/", uri);
        protected override void NavigateToCore(string uri, bool forceLoad) { Target = uri; ForceLoad = forceLoad; }
    }

    private sealed class CompletedAuthentication : IAuthenticationService
    {
        public TwoFactorChallengeResponse? PendingChallenge => null;
        public string? ErrorMessage => null;
        public event Action<string?>? LoginChange { add { } remove { } }
        public Task<bool> LoginAsync(TokenRequest model) => Task.FromResult(true);
        public Task<bool> LogoutAsync(string methodName) => Task.FromResult(true);
        public Task<bool> VerifyTwoFactorAsync(string code, bool recovery) => throw new NotSupportedException();
        public Task<bool> RefreshAsync() => throw new NotSupportedException();
        public void CancelTwoFactor() => throw new NotSupportedException();
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
