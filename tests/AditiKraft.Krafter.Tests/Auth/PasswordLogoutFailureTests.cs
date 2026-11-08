using System.Net;
using System.Net.Http.Json;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Refit;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class PasswordLogoutFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulPasswordMutationOffersALogoutRetryWithoutRepeatingTheMutation(bool reset)
    {
        using var transport = new PasswordTransport(reset);
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://app.example.test") };
        var authentication = new Authentication();
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton<NavigationManager, Navigation>();
        registrations.AddSingleton<IJSRuntime, NoJavaScript>();
        registrations.AddSingleton<IAuthenticationService>(authentication);
        registrations.AddSingleton(RestService.For<IUsersApi>(http));
        registrations.AddSingleton<NotificationService>();
        registrations.AddSingleton<ApiCallService>();
        registrations.AddSingleton<Components>();
        registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<Components>());
        await using ServiceProvider services = registrations.BuildServiceProvider();
        Components components = services.GetRequiredService<Components>();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = reset
                ? await renderer.RenderComponentAsync<ResetPassword>()
                : await renderer.RenderComponentAsync<ChangePassword>();
            if (reset)
            {
                var form = components.All.OfType<RadzenTemplateForm<ResetPasswordRequest>>().Single();
                await form.Submit.InvokeAsync(form.Data);
            }
            else
            {
                var form = components.All.OfType<RadzenTemplateForm<ChangePasswordRequest>>().Single();
                await form.Submit.InvokeAsync(form.Data);
            }

            Assert.Equal(1, transport.Mutations);
            Assert.Equal(1, authentication.Logouts);
            Assert.Contains("Unable to sign out. Please try again.", root.ToHtmlString());
            Assert.Contains(reset ? "Your password has been reset" : "Your password has been changed", root.ToHtmlString());
            RadzenButton retry = components.All.OfType<RadzenButton>().Last(button => button.Text == "Sign in again");
            Assert.False(retry.IsBusy);

            authentication.FailLogout = false;
            await retry.Click.InvokeAsync(new MouseEventArgs());

            Assert.Equal(1, transport.Mutations);
            Assert.Equal(2, authentication.Logouts);
            Assert.DoesNotContain("Unable to sign out", root.ToHtmlString());
        });
    }

    private sealed class PasswordTransport(bool reset) : HttpMessageHandler
    {
        public int Mutations { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(reset ? "/api/users/reset-password" : "/api/users/change-password", request.RequestUri!.AbsolutePath);
            Mutations++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Response.Success()) });
        }
    }

    private sealed class Authentication : IAuthenticationService
    {
        public bool FailLogout { get; set; } = true;
        public int Logouts { get; private set; }
        public TwoFactorChallengeResponse? PendingChallenge => null;
        public string? ErrorMessage => FailLogout ? "Unable to sign out. Please try again." : null;
        public event Action<string?>? LoginChange { add { } remove { } }
        public Task<bool> LogoutAsync(string methodName) { Logouts++; return Task.FromResult(!FailLogout); }
        public Task<bool> LoginAsync(TokenRequest model) => throw new NotSupportedException();
        public Task<bool> VerifyTwoFactorAsync(string code, bool recovery) => throw new NotSupportedException();
        public Task<bool> RefreshAsync() => throw new NotSupportedException();
        public void CancelTwoFactor() => throw new NotSupportedException();
    }

    private sealed class Components(IServiceProvider services) : IComponentActivator
    {
        public List<IComponent> All { get; } = [];
        public IComponent CreateInstance(Type type)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, type);
            All.Add(component);
            return component;
        }
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://app.example.test/", "https://app.example.test/account/change-password");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
