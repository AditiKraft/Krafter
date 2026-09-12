using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Realtime;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.SignalR.Client;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;

public class SignalRService : IAsyncDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IAuthenticationService _authenticationService;
    private readonly IFormFactor _formatProvider;
    private readonly IAuthStorageService _localStorageService;
    private HubConnection? _hubConnection;

    public event Action<string, string>? MessageReceived;

    public SignalRService(IAuthenticationService authenticationService, IAuthStorageService localStorageService,
        IFormFactor formatProvider, AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _authenticationService = authenticationService;
        _formatProvider = formatProvider;
        _localStorageService = localStorageService;
    }

    public async Task InitializeAsync()
    {
        string formFactorType = _formatProvider.GetFormFactor();

        if (formFactorType != "WebAssembly")
        {
            return;
        }

        AuthenticationState authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        bool isAuthenticated = authState.User.Identity?.IsAuthenticated ?? false;
        if (isAuthenticated)
        {
            _hubConnection = new HubConnectionBuilder()
                // Browser WebSocket connections cannot send a custom tenant header.
                .WithUrl(TenantInfo.HostUrl + $"/{ApiRoutes.ApiPrefix}/RealtimeHub?tenantIdentifier={Uri.EscapeDataString(TenantInfo.Identifier)}", options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        string? token = await _localStorageService.GetCachedAuthTokenAsync();
                        if (AuthTokenService.NeedsRefresh(token))
                        {
                            if (!await _authenticationService.RefreshAsync())
                            {
                                await _authenticationService.LogoutAsync(nameof(SignalRService));
                                return null;
                            }

                            token = await _localStorageService.GetCachedAuthTokenAsync();
                        }

                        return token?.Replace("Bearer ", "").Trim();
                    };
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) })
                .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug); })
                .Build();

            _hubConnection.Closed += async (exception) =>
            {
                Console.WriteLine($"Connection closed: {exception?.Message}");
                await Task.CompletedTask;
            };
            _hubConnection.On<string, string>(nameof(SignalRMethods.ReceiveMessage),
                (user, message) => { MessageReceived?.Invoke(user, message); });
            await _hubConnection.StartAsync();
        }
    }

    public async Task SendMessageAsync(string user, string message)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync(nameof(SignalRMethods.SendMessage), user, message);
        }
    }

    public bool IsConnected() => _hubConnection?.State == HubConnectionState.Connected;

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
