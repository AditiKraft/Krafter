using System.Reflection;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class ReturnUrlTests
{
    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("/users", "/users")]
    [InlineData("/users?filter=active#details", "/users?filter=active#details")]
    [InlineData("/users?query=line%0Abreak", "/users?query=line%0Abreak")]
    [InlineData("https://redirect.invalid/after-login", "/")]
    [InlineData("//redirect.invalid/after-login", "/")]
    [InlineData("/\\redirect.invalid/after-login", "/")]
    [InlineData("/\t/redirect.invalid/after-login", "/")]
    [InlineData("/\n/redirect.invalid/after-login", "/")]
    [InlineData("/\r/redirect.invalid/after-login", "/")]
    [InlineData("/users?query=\0value", "/")]
    [InlineData("/users#\u007fvalue", "/")]
    [InlineData("/LOGIN?ReturnUrl=/users", "/")]
    [InlineData("/Account/Login", "/")]
    public async Task AllLoginCompletionPathsUseOnlySafeLocalTargets(string? returnUrl, string expected)
    {
        foreach (string flow in new[] { "password", "two-factor", "already-authenticated" })
        {
            var navigation = new RecordingNavigation();
            var page = new Login(new CompletedAuthentication(), null!, navigation, null!,
                new AppUrls { RootUiUrl = "https://app.example" }, null!);
            typeof(Login).GetProperty(nameof(Login.ReturnUrl))!.SetValue(page, returnUrl);
            if (flow == "already-authenticated")
            {
                typeof(Login).GetField("_shouldRedirect", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, true);
                Method("OnAfterRender").Invoke(page, [false]);
            }
            else
            {
                object request = flow == "password" ? new TokenRequest() : new TwoFactorRequest();
                await (Task)Method(flow == "password" ? "CreateToken" : "VerifyTwoFactor").Invoke(page, [request])!;
            }
            Assert.Equal(expected, navigation.Target);
        }
    }

    [Theory]
    [InlineData("/%09/redirect.invalid/after-login")]
    [InlineData("/%0a/redirect.invalid/after-login")]
    [InlineData("/%0d/redirect.invalid/after-login")]
    public async Task QueryDecodedControlsCannotChangeThePostLoginOrigin(string encoded)
    {
        string decoded = QueryHelpers.ParseQuery("?ReturnUrl=" + encoded)["ReturnUrl"].ToString();
        Assert.Contains(decoded, char.IsControl);
        await AllLoginCompletionPathsUseOnlySafeLocalTargets(decoded, "/");
    }

    private static MethodInfo Method(string name) => typeof(Login).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class RecordingNavigation : NavigationManager
    {
        public string? Target { get; private set; }
        public RecordingNavigation() => Initialize("https://app.example/", "https://app.example/login");
        protected override void NavigateToCore(string uri, bool forceLoad) => Target = uri;
    }

    private sealed class CompletedAuthentication : IAuthenticationService
    {
        public TwoFactorChallengeResponse? PendingChallenge => null;
        public string? ErrorMessage => null;
        public void CancelTwoFactor() =>
            throw new NotSupportedException("This test double does not exercise MFA.");

        public event Action<string?>? LoginChange { add { } remove { } }
        public Task<bool> LoginAsync(TokenRequest model) => Task.FromResult(true);
        public Task<bool> VerifyTwoFactorAsync(string code, bool recovery) => Task.FromResult(true);
        public Task<bool> LogoutAsync(string methodName) => Task.FromResult(true);
        public Task<bool> RefreshAsync() => Task.FromResult(false);
    }
}
