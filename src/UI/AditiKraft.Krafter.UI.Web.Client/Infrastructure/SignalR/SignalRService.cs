using System.IdentityModel.Tokens.Jwt;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.AspNetCore.SignalR.Client;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;

public class SignalRService : IAsyncDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IAuthenticationService _authenticationService;
    private readonly IFormFactor _formatProvider;
    private readonly IKrafterLocalStorageService _localStorageService;
    private HubConnection? _hubConnection;

    public event Action<string, string>? MessageReceived;

    public SignalRService(IAuthenticationService authenticationService, IKrafterLocalStorageService localStorageService,
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
                .WithUrl(TenantInfo.HostUrl + "/RealtimeHub", options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        string? token =
                            await _localStorageService
                                .GetCachedAuthTokenAsync(); //_authenticationService.GetJwtAsync();

                        // Check if the token is expired
                        if (string.IsNullOrEmpty(token) || IsTokenExpired(token))
                        {
                            // Wait for 2 seconds
                            await Task.Delay(2000);

                            // Try to get the token again
                            token = await _localStorageService.GetCachedAuthTokenAsync();

                            // Check again if the token is expired
                            if (string.IsNullOrEmpty(token) || IsTokenExpired(token))
                            {
                                // Attempt to refresh the token
                                bool refreshResult = await _authenticationService.RefreshAsync();
                                if (refreshResult)
                                {
                                    token = await _localStorageService.GetCachedAuthTokenAsync();
                                }
                                else
                                {
                                    // Handle the case when refreshing the token fails
                                    await _authenticationService.LogoutAsync("SignalRService 71");
                                    return null;
                                }
                            }
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

    private bool IsTokenExpired(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (handler.ReadToken(token) is JwtSecurityToken jwtToken)
        {
            return jwtToken.ValidTo < DateTime.UtcNow;
        }

        return true;
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


