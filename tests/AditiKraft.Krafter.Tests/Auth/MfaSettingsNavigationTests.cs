using System.Reflection;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.Auth;

[Collection("Client realtime configuration")]
public sealed class MfaSettingsNavigationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly string[] TestCodes = Enumerable.Range(0, 10).Select(index => $"test-recovery-code-{index:00}").ToArray();
    private const string SignInButton = "I saved my codes — sign in again";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingAndUnsavedRecoveryResultsBlockNavigationAndGlobalLogout(bool enrollment)
    {
        await using var page = new SettingsPage(enabled: !enrollment);
        await page.RenderAsync();
        Assert.True(await page.CanNavigateAsync());
        var response = new TaskCompletionSource<Response<RecoveryCodesResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Api.SecretResponse = () => response.Task;
        Task operation;
        if (enrollment)
        {
            await page.ClickAsync("Set up authenticator");
            operation = await page.StartAsync(() => page.Form.Submit.InvokeAsync(page.Form.Data));
        }
        else
        {
            await page.RunAsync(async () =>
            {
                await page.TextBox("ManagementCode").ValueChanged.InvokeAsync("test-final-recovery-code");
                await page.CheckBox("ManagementRecovery").ValueChanged.InvokeAsync(true);
            });
            operation = await page.StartAsync(() => page.Button("Generate new recovery codes").Click.InvokeAsync(new MouseEventArgs()));
            // Leaving while the confirmation is open must not let its continuation mutate a disposed page.
            Assert.False(await page.CanNavigateAsync());
            await page.RunAsync(() => { page.Dialogs.Close(true); return Task.CompletedTask; });
        }

        try
        {
            await page.Api.SecretRequestStarted.Task.WaitAsync(Timeout);
            Assert.False(await page.CanNavigateAsync());
            Assert.True(page.Lock.ConfirmExternalNavigation);
            await page.GlobalLogoutAsync();
            Assert.Equal(0, page.Authentication.LogoutCount);
            Assert.Null(page.Navigation.LastTarget);
            Assert.Contains(page.Notifications.Messages, message => message.Detail?.Contains("wait for the account security request") == true);
            if (!enrollment)
            {
                Assert.True(page.Api.UsedRecoveryCode);
                Assert.Equal("test-final-recovery-code", page.Api.ManagementCode);
            }
        }
        finally
        {
            response.TrySetResult(Response<RecoveryCodesResponse>.Success(new(TestCodes)));
            await operation.WaitAsync(Timeout);
        }

        Assert.False(await page.CanNavigateAsync());
        Assert.True(page.Lock.ConfirmExternalNavigation);
        await page.GlobalLogoutAsync();
        Assert.Equal(0, page.Authentication.LogoutCount);
        await page.RunAsync(async () =>
        {
            Assert.Contains("Save your recovery codes", page.Root.ToHtmlString());
            Assert.True(page.Button(SignInButton).Disabled);
            // The handler must also enforce acknowledgment if invoked without the disabled DOM button.
            await page.Button(SignInButton).Click.InvokeAsync(new MouseEventArgs());
        });
        Assert.Equal(0, page.Authentication.LogoutCount);

        await page.ClickAsync("Download recovery codes");
        Assert.Equal(string.Join(Environment.NewLine, TestCodes), Assert.Single(page.JavaScript.Downloads));
        Assert.False(await page.CanNavigateAsync());
        await page.RunAsync(() => page.CheckBox("RecoveryCodesSaved").ValueChanged.InvokeAsync(true));
        Assert.True(await page.CanNavigateAsync());
        Assert.False(page.Lock.ConfirmExternalNavigation);
        await page.ClickAsync(SignInButton);
        Assert.Equal(1, page.Authentication.LogoutCount);
        Assert.False(page.Authentication.LoggedOutWhileBlocked);
        Assert.Equal("/login", page.Navigation.LastTarget);
    }

    [Fact]
    public async Task CancelingRegenerationConfirmationReleasesTheGuardWithoutCallingTheApi()
    {
        await using var page = new SettingsPage(enabled: true);
        await page.RenderAsync();
        Task operation = await page.StartAsync(() => page.Button("Generate new recovery codes").Click.InvokeAsync(new MouseEventArgs()));
        Assert.False(await page.CanNavigateAsync());
        await page.RunAsync(() => { page.Dialogs.Close(false); return Task.CompletedTask; });
        await operation.WaitAsync(Timeout);
        Assert.Equal(0, page.Api.RegenerationCount);
        Assert.True(await page.CanNavigateAsync());
        Assert.False(page.Lock.ConfirmExternalNavigation);
        Assert.Null(page.Guard.BlockingMessage);
    }

    [Fact]
    public async Task CancelingReplacementReloadsTheCountAfterUsingTheLastRecoveryCode()
    {
        await using var page = new SettingsPage(enabled: true);
        await page.RenderAsync();
        await page.RunAsync(async () =>
        {
            Assert.Contains("Recovery codes remaining: 1", page.Root.ToHtmlString());
            await page.TextBox("ManagementCode").ValueChanged.InvokeAsync("test-final-recovery-code");
            await page.CheckBox("ManagementRecovery").ValueChanged.InvokeAsync(true);
        });

        await page.ClickAsync("Replace authenticator");
        Assert.True(page.Api.UsedRecoveryCode);
        Assert.Equal("test-final-recovery-code", page.Api.ManagementCode);
        Assert.Equal(0, page.Api.RemainingRecoveryCodes);
        await page.RunAsync(() =>
        {
            Assert.Contains("Scan with your authenticator", page.Root.ToHtmlString());
            return Task.CompletedTask;
        });

        await page.ClickAsync("Cancel setup");

        Assert.Equal(1, page.Api.StatusCount);
        await page.RunAsync(() =>
        {
            Assert.Contains("Recovery codes remaining: 0", page.Root.ToHtmlString());
            Assert.DoesNotContain("Recovery codes remaining: 1", page.Root.ToHtmlString());
            Assert.DoesNotContain("Scan with your authenticator", page.Root.ToHtmlString());
            Assert.Empty(page.TextBox("ManagementCode").Value);
            Assert.False(page.Button("Replace authenticator").IsBusy);
            return Task.CompletedTask;
        });
        Assert.True(await page.CanNavigateAsync());
    }

    [Fact]
    public async Task FailedStatusRefreshAfterCancelDoesNotRestoreTheConsumedRecoveryCodeCount()
    {
        await using var page = new SettingsPage(enabled: true);
        await page.RenderAsync();
        await page.RunAsync(async () =>
        {
            await page.TextBox("ManagementCode").ValueChanged.InvokeAsync("test-final-recovery-code");
            await page.CheckBox("ManagementRecovery").ValueChanged.InvokeAsync(true);
        });
        await page.ClickAsync("Replace authenticator");
        Assert.Equal(0, page.Api.RemainingRecoveryCodes);
        var response = new TaskCompletionSource<Response<TwoFactorStatusResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Api.StatusResponse = () => response.Task;
        Task pending = await page.StartAsync(() => page.Button("Cancel setup").Click.InvokeAsync(new MouseEventArgs()));

        try
        {
            await page.RunAsync(async () =>
            {
                Assert.DoesNotContain("Recovery codes remaining:", page.Root.ToHtmlString());
                Assert.DoesNotContain("Scan with your authenticator", page.Root.ToHtmlString());
                // Repeated callbacks cannot issue a second refresh while cancellation is busy.
                await page.Button("Cancel setup").Click.InvokeAsync(new MouseEventArgs());
            });
            Assert.Equal(1, page.Api.StatusCount);
            Assert.True(await page.CanNavigateAsync());
        }
        finally
        {
            response.TrySetResult(new Response<TwoFactorStatusResponse>
            {
                IsError = true,
                StatusCode = 503,
                Message = "Unable to load account security. Please try again.",
                Data = new(true, 1, true)
            });
            await pending.WaitAsync(Timeout);
        }

        await page.RunAsync(() =>
        {
            Assert.Contains("role=\"alert\"", page.Root.ToHtmlString());
            Assert.Contains("Unable to load account security. Please try again.", page.Root.ToHtmlString());
            Assert.Contains("Account security is unavailable. Reload this page to try again.", page.Root.ToHtmlString());
            Assert.DoesNotContain("Loading account security", page.Root.ToHtmlString());
            Assert.DoesNotContain("Recovery codes remaining:", page.Root.ToHtmlString());
            Assert.DoesNotContain("Scan with your authenticator", page.Root.ToHtmlString());
            return Task.CompletedTask;
        });
        Assert.Null(page.Guard.BlockingMessage);
        Assert.True(await page.CanNavigateAsync());
    }

    [Theory]
    [InlineData("setup")]
    [InlineData("confirm")]
    [InlineData("regenerate")]
    public async Task FailedSecretRequestReleasesNavigationWithoutClaimingRollback(string operation)
    {
        await using var page = new SettingsPage(enabled: operation == "regenerate");
        await page.RenderAsync();
        page.Api.SecretResponse = () => Task.FromResult(Response<RecoveryCodesResponse>.CustomError("Authentication timed out. Please try again.", 503));
        if (operation == "setup")
        {
            page.Api.SetupResponse = () => Task.FromResult(Response<TwoFactorSetupResponse>.CustomError("Unable to reach the server.", 503));
            await page.ClickAsync("Set up authenticator");
        }
        else if (operation == "confirm")
        {
            await page.ClickAsync("Set up authenticator");
            await page.RunAsync(() => page.Form.Submit.InvokeAsync(page.Form.Data));
        }
        else
        {
            Task pending = await page.StartAsync(() => page.Button("Generate new recovery codes").Click.InvokeAsync(new MouseEventArgs()));
            await page.RunAsync(() => { page.Dialogs.Close(true); return Task.CompletedTask; });
            await pending.WaitAsync(Timeout);
        }

        Assert.True(await page.CanNavigateAsync());
        Assert.False(page.Lock.ConfirmExternalNavigation);
        Assert.Null(page.Guard.BlockingMessage);
        await page.RunAsync(() =>
        {
            Assert.DoesNotContain("Save your recovery codes</h2>", page.Root.ToHtmlString());
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task FailedDownloadKeepsCodesAndGuardUntilManualSaveIsAcknowledged()
    {
        await using var page = new SettingsPage(enabled: false);
        await page.RenderAsync();
        await page.ClickAsync("Set up authenticator");
        await page.RunAsync(() => page.Form.Submit.InvokeAsync(page.Form.Data));
        page.JavaScript.FailDownloads = true;
        await page.ClickAsync("Download recovery codes");
        Assert.False(await page.CanNavigateAsync());
        await page.RunAsync(() =>
        {
            Assert.Contains("Unable to start the download", page.Root.ToHtmlString());
            Assert.Contains(TestCodes[0], page.Root.ToHtmlString());
            Assert.True(page.Button(SignInButton).Disabled);
            return Task.CompletedTask;
        });
        await page.RunAsync(() => page.CheckBox("RecoveryCodesSaved").ValueChanged.InvokeAsync(true));
        Assert.True(await page.CanNavigateAsync());
        await page.GlobalLogoutAsync();
        Assert.Equal(1, page.Authentication.LogoutCount);
        Assert.False(page.Authentication.LoggedOutWhileBlocked);
    }

    [Fact]
    public async Task FailedOptionalRealtimeRefreshPreservesUnsavedRecoveryCodes()
    {
        await VerifyOptionalRealtimeFailureAsync(pending: false);
    }

    [Fact]
    public async Task FailedOptionalRealtimeRefreshPreservesPendingRecoveryRequest()
    {
        await VerifyOptionalRealtimeFailureAsync(pending: true);
    }

    private static async Task VerifyOptionalRealtimeFailureAsync(bool pending)
    {
        await using var page = new SettingsPage(enabled: false);
        await page.RenderAsync();
        var response = new TaskCompletionSource<Response<RecoveryCodesResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Api.SecretResponse = () => response.Task;
        await page.ClickAsync("Set up authenticator");
        Task operation = await page.StartAsync(() => page.Form.Submit.InvokeAsync(page.Form.Data));
        try
        {
            await page.Api.SecretRequestStarted.Task.WaitAsync(Timeout);
            if (!pending)
            {
                response.TrySetResult(Response<RecoveryCodesResponse>.Success(new(TestCodes)));
                await operation.WaitAsync(Timeout);
            }
            Assert.False(await page.CanNavigateAsync());
            string? blockingMessage = page.Guard.BlockingMessage;
            Assert.NotNull(blockingMessage);

            await using ClientRealtimeLifecycleTests.TestHost host = await ClientRealtimeLifecycleTests.TestHost.StartAsync(401);
            var storage = new ClientRealtimeLifecycleTests.Storage { Tokens = TokenTestData.Create(expired: true) };
            var api = new ClientRealtimeLifecycleTests.TokenApi
            {
                Refresh = _ => Task.FromResult(Response<TokenResponse>.Unauthorized("Session revoked"))
            };
            using var coordinator = new TokenRefreshCoordinator();
            await using var realtime = new SignalRService(new AuthTokenService(api, storage, coordinator), storage,
                new ClientRealtimeLifecycleTests.FormFactor(), new ClientRealtimeLifecycleTests.StateProvider(true, true), NullLogger<SignalRService>.Instance);
            // Exercise the same token callback used by startup and an in-flight reconnect after transport loss.
            // A normal, delivered server Context.Abort close deliberately forbids reconnect.
            await page.RunAsync(realtime.InitializeAsync).WaitAsync(Timeout);

            Assert.False(realtime.IsConnected());
            Assert.True(api.Refreshes > 0);
            Assert.True(host.Observations.Requests > 0);
            Assert.Equal(0, api.Logouts);
            Assert.Equal(0, storage.Clears);
            Assert.Equal(0, storage.Saves);
            Assert.Equal(0, page.Authentication.LogoutCount);
            Assert.Null(page.Navigation.LastTarget);
            Assert.Equal(blockingMessage, page.Guard.BlockingMessage);
            Assert.False(await page.CanNavigateAsync());
            if (pending) Assert.False(operation.IsCompleted);
            else
            {
                await page.RunAsync(() =>
                {
                    Assert.Contains(TestCodes[0], page.Root.ToHtmlString());
                    Assert.True(page.Button(SignInButton).Disabled);
                    return Task.CompletedTask;
                });
            }
        }
        finally
        {
            response.TrySetResult(Response<RecoveryCodesResponse>.Success(new(TestCodes)));
            await operation.WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task VerifiedDisableCanPerformItsOwnLogoutWithoutASavedCodesCheckbox()
    {
        await using var page = new SettingsPage(enabled: true);
        await page.RenderAsync();
        Task pending = await page.StartAsync(() => page.Button("Disable two-factor authentication").Click.InvokeAsync(new MouseEventArgs()));
        await page.RunAsync(() => { page.Dialogs.Close(true); return Task.CompletedTask; });
        await pending.WaitAsync(Timeout);
        Assert.Equal(1, page.Authentication.LogoutCount);
        Assert.False(page.Authentication.LoggedOutWhileBlocked);
    }

    [Fact]
    public async Task FailedLogoutKeepsAcknowledgedRecoveryCodesVisibleUntilRetrySucceeds()
    {
        await using var page = new SettingsPage(enabled: false);
        await page.RenderAsync();
        await page.ClickAsync("Set up authenticator");
        await page.RunAsync(() => page.Form.Submit.InvokeAsync(page.Form.Data));
        await page.RunAsync(() => page.CheckBox("RecoveryCodesSaved").ValueChanged.InvokeAsync(true));
        page.Authentication.FailLogout = true;

        await page.ClickAsync(SignInButton);

        Assert.Equal(1, page.Authentication.LogoutCount);
        Assert.Null(page.Navigation.LastTarget);
        await page.RunAsync(() =>
        {
            Assert.Contains(TestCodes[0], page.Root.ToHtmlString());
            Assert.Contains("Unable to sign out. Please try again.", page.Root.ToHtmlString());
            Assert.False(page.Button(SignInButton).IsBusy);
            Assert.False(page.Button(SignInButton).Disabled);
            return Task.CompletedTask;
        });

        page.Authentication.FailLogout = false;
        await page.ClickAsync(SignInButton);
        Assert.Equal(2, page.Authentication.LogoutCount);
        Assert.Equal("/login", page.Navigation.LastTarget);
        await page.RunAsync(() =>
        {
            Assert.DoesNotContain(TestCodes[0], page.Root.ToHtmlString());
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task FailedLogoutAfterDisableCanRetryWithoutRepeatingTheMutation()
    {
        await using var page = new SettingsPage(enabled: true);
        await page.RenderAsync();
        page.Authentication.FailLogout = true;
        Task pending = await page.StartAsync(() => page.Button("Disable two-factor authentication").Click.InvokeAsync(new MouseEventArgs()));
        await page.RunAsync(() => { page.Dialogs.Close(true); return Task.CompletedTask; });
        await pending.WaitAsync(Timeout);

        Assert.Equal(1, page.Api.DisableCount);
        Assert.Equal(1, page.Authentication.LogoutCount);
        Assert.Null(page.Navigation.LastTarget);
        await page.RunAsync(() =>
        {
            Assert.Contains("Two-factor authentication has been disabled", page.Root.ToHtmlString());
            Assert.Contains("Unable to sign out. Please try again.", page.Root.ToHtmlString());
            return Task.CompletedTask;
        });

        page.Authentication.FailLogout = false;
        await page.ClickAsync("Sign in again");
        Assert.Equal(1, page.Api.DisableCount);
        Assert.Equal(2, page.Authentication.LogoutCount);
        Assert.Equal("/login", page.Navigation.LastTarget);
    }

    [Fact]
    public async Task DisposingThePageRemovesItsScopedLogoutBlocker()
    {
        var page = new SettingsPage(enabled: false);
        try
        {
            await page.RenderAsync();
            await page.ClickAsync("Set up authenticator");
            await page.RunAsync(() => page.Form.Submit.InvokeAsync(page.Form.Data));
            Assert.NotNull(page.Guard.BlockingMessage);
        }
        finally
        {
            await page.DisposeAsync();
        }
        Assert.Null(page.Guard.BlockingMessage);
    }

    private sealed class SettingsPage : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CapturingActivator components;
        private readonly HtmlRenderer renderer;
        public TwoFactorApi Api { get; } = new();
        public TestNavigation Navigation { get; } = new();
        public DownloadJavaScript JavaScript { get; } = new();
        public AccountSecurityNavigationGuard Guard { get; } = new();
        public NotificationService Notifications { get; } = new();
        public Authentication Authentication { get; }
        public DialogService Dialogs { get; }
        public HtmlRootComponent Root { get; private set; }

        public SettingsPage(bool enabled)
        {
            Authentication = new Authentication(Navigation, Guard);
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddSingleton<NavigationManager>(Navigation);
            registrations.AddSingleton<IJSRuntime>(JavaScript);
            registrations.AddSingleton<ITwoFactorApi>(Api);
            registrations.AddSingleton<IAuthenticationService>(Authentication);
            registrations.AddSingleton(Guard);
            registrations.AddSingleton(Notifications);
            registrations.AddSingleton<DialogService>();
            registrations.AddSingleton<CapturingActivator>(provider => new CapturingActivator(provider, enabled));
            registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<CapturingActivator>());
            services = registrations.BuildServiceProvider();
            Dialogs = services.GetRequiredService<DialogService>();
            components = services.GetRequiredService<CapturingActivator>();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public NavigationLock Lock => components.All.OfType<NavigationLock>().Single();
        public RadzenTemplateForm<TwoFactorRequest> Form => components.All.OfType<RadzenTemplateForm<TwoFactorRequest>>().Last();
        public RadzenButton Button(string text) => components.All.OfType<RadzenButton>().Last(button => button.Text == text);
        public RadzenTextBox TextBox(string name) => components.All.OfType<RadzenTextBox>().Last(input => input.Name == name);
        public RadzenCheckBox<bool> CheckBox(string name) => components.All.OfType<RadzenCheckBox<bool>>().Last(input => input.Name == name);
        public Task RunAsync(Func<Task> action) => renderer.Dispatcher.InvokeAsync(action);
        public Task RenderAsync() => RunAsync(async () => Root = await renderer.RenderComponentAsync<TwoFactorSettings>());
        public Task ClickAsync(string text) => RunAsync(() => Button(text).Click.InvokeAsync(new MouseEventArgs()));

        public async Task<Task> StartAsync(Func<Task> action)
        {
            Task pending = Task.CompletedTask;
            await RunAsync(() => { pending = action(); return Task.CompletedTask; });
            return pending;
        }

        public Task<bool> CanNavigateAsync() => renderer.Dispatcher.InvokeAsync(async () =>
        {
            // HtmlRenderer intentionally omits after-render callbacks; run the real lock's lifecycle explicitly.
            await ((IHandleAfterRender)Lock).OnAfterRenderAsync();
            return await Navigation.CanNavigateAsync("https://app.example/");
        });

        public Task GlobalLogoutAsync() => RunAsync(async () =>
        {
            var menu = new TopRight(Authentication, Navigation, Guard, Notifications);
            MethodInfo click = typeof(TopRight).GetMethod("SplitButtonClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var item = new RadzenSplitButtonItem();
            ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(RadzenSplitButtonItem.Value)] = "Logout"
            }).SetParameterProperties(item);
            await (Task)click.Invoke(menu, [item])!;
        });

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await services.DisposeAsync();
        }
    }

    private sealed class CapturingActivator(IServiceProvider services, bool enabled) : IComponentActivator
    {
        public List<IComponent> All { get; } = [];
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, componentType);
            if (component is TwoFactorSettings)
            {
                // Initial browser-only status loading is replaced with a fixture; all user event handlers remain real.
                typeof(TwoFactorSettings).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(component, new TwoFactorStatusResponse(enabled, 1, true));
            }
            All.Add(component);
            return component;
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public string? LastTarget { get; private set; }
        public TestNavigation() => Initialize("https://app.example/", "https://app.example/account/two-factor");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastTarget = uri;
        protected override void SetNavigationLockState(bool value) { }
        public ValueTask<bool> CanNavigateAsync(string target) => NotifyLocationChangingAsync(target, null, true);
    }

    private sealed class DownloadJavaScript : IJSRuntime
    {
        public List<string> Downloads { get; } = [];
        public bool FailDownloads { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "krafterDownloadRecoveryCodes")
            {
                if (FailDownloads) throw new JSException("Test download failure");
                Downloads.Add((string)args![0]!);
            }
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class Authentication(TestNavigation navigation, AccountSecurityNavigationGuard guard) : IAuthenticationService
    {
        public int LogoutCount { get; private set; }
        public bool FailLogout { get; set; }
        public bool LoggedOutWhileBlocked { get; private set; }
        public TwoFactorChallengeResponse? PendingChallenge => null;
        public string? ErrorMessage => FailLogout ? "Unable to sign out. Please try again." : null;
        public event Action<string?>? LoginChange { add { } remove { } }
        public Task<bool> LogoutAsync(string methodName)
        {
            LogoutCount++;
            LoggedOutWhileBlocked |= guard.BlockingMessage is not null;
            if (FailLogout) return Task.FromResult(false);
            navigation.NavigateTo("/login", true);
            return Task.FromResult(true);
        }
        public Task<bool> LoginAsync(TokenRequest model) => throw new NotSupportedException();
        public Task<bool> VerifyTwoFactorAsync(string code, bool recovery) => throw new NotSupportedException();
        public void CancelTwoFactor() => throw new NotSupportedException();
        public Task<bool> RefreshAsync() => throw new NotSupportedException();
    }

    private sealed class TwoFactorApi : ITwoFactorApi
    {
        public TaskCompletionSource SecretRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Task<Response<TwoFactorSetupResponse>>> SetupResponse { get; set; } = () => Task.FromResult(
            Response<TwoFactorSetupResponse>.Success(new("test-enrollment", "test-key", "<svg xmlns='http://www.w3.org/2000/svg'/>", DateTimeOffset.UtcNow.AddMinutes(5))));
        public Func<Task<Response<RecoveryCodesResponse>>> SecretResponse { get; set; } = () => Task.FromResult(Response<RecoveryCodesResponse>.Success(new(TestCodes)));
        public Func<Task<Response<TwoFactorStatusResponse>>>? StatusResponse { get; set; }
        public int RegenerationCount { get; private set; }
        public int DisableCount { get; private set; }
        public int StatusCount { get; private set; }
        public int RemainingRecoveryCodes { get; private set; } = 1;
        public bool UsedRecoveryCode { get; private set; }
        public string? ManagementCode { get; private set; }
        public Task<Response<TwoFactorStatusResponse>> StatusAsync(CancellationToken cancellationToken = default)
        {
            StatusCount++;
            return StatusResponse?.Invoke() ?? Task.FromResult(Response<TwoFactorStatusResponse>.Success(new(true, RemainingRecoveryCodes, true)));
        }
        public async Task<Response<TwoFactorSetupResponse>> SetupAsync(TwoFactorManagementRequest request, CancellationToken cancellationToken = default)
        {
            UsedRecoveryCode = request.UseRecoveryCode;
            ManagementCode = request.Code;
            Response<TwoFactorSetupResponse> response = await SetupResponse();
            if (!response.IsError && request.UseRecoveryCode)
            {
                // Model the server consuming successful recovery proof before returning enrollment material.
                RemainingRecoveryCodes--;
            }
            return response;
        }
        public Task<Response<RecoveryCodesResponse>> ConfirmAsync(TwoFactorRequest request, CancellationToken cancellationToken = default)
        {
            SecretRequestStarted.TrySetResult();
            return SecretResponse();
        }
        public Task<Response> DisableAsync(TwoFactorManagementRequest request, CancellationToken cancellationToken = default)
        {
            DisableCount++;
            return Task.FromResult(Response.Success());
        }
        public Task<Response<RecoveryCodesResponse>> RegenerateAsync(TwoFactorManagementRequest request, CancellationToken cancellationToken = default)
        {
            RegenerationCount++;
            UsedRecoveryCode = request.UseRecoveryCode;
            ManagementCode = request.Code;
            SecretRequestStarted.TrySetResult();
            return SecretResponse();
        }
    }
}
