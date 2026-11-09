using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Realtime;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.SignalR.Client;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;

public class SignalRService : IAsyncDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly AuthTokenService _tokenService;
    private readonly IFormFactor _formatProvider;
    private readonly IAuthStorageService _localStorageService;
    private readonly ILogger<SignalRService> _logger;
    private readonly object _lifecycleLock = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private CancellationTokenSource? _initializationCancellation;
    private CancellationTokenSource? _connectionCancellation;
    private HubConnection? _hubConnection;
    private Task? _disposeTask;

    public event Action<string, string>? MessageReceived;

    public SignalRService(AuthTokenService tokenService, IAuthStorageService localStorageService,
        IFormFactor formatProvider, AuthenticationStateProvider authenticationStateProvider, ILogger<SignalRService> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _tokenService = tokenService;
        _formatProvider = formatProvider;
        _localStorageService = localStorageService;
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        lock (_lifecycleLock)
        {
            if (_disposeTask is not null) return Task.CompletedTask;
            // A later login/logout supersedes any connection still being started.
            _initializationCancellation?.Cancel();
            _connectionCancellation?.Cancel();
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            _initializationCancellation = cancellation;
            return InitializeCoreAsync(cancellation);
        }
    }

    private async Task InitializeCoreAsync(CancellationTokenSource cancellation)
    {
        bool acquired = false;
        bool started = false;
        try
        {
            await _connectionLock.WaitAsync(cancellation.Token);
            acquired = true;
            await DisposeConnectionAsync();
            cancellation.Token.ThrowIfCancellationRequested();
            if (_formatProvider.GetFormFactor() != "WebAssembly") return;

            AuthenticationState authState = await _authenticationStateProvider.GetAuthenticationStateAsync()
                .WaitAsync(cancellation.Token);
            // This is a display gate. The hub independently checks current server-side permissions.
            if (authState.User.Identity?.IsAuthenticated != true ||
                !authState.User.HasClaim(AppClaimTypes.Permission,
                    PermissionDefinition.NameFor(PermissionAction.View, PermissionResource.Notifications))) return;

            CancellationToken connectionToken;
            lock (_lifecycleLock)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                _connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                // This token belongs to the connection, including reconnects after initialization ends.
                connectionToken = _connectionCancellation.Token;
            }
            _hubConnection = new HubConnectionBuilder()
                // Browser WebSocket connections cannot send a custom tenant header.
                .WithUrl(TenantInfo.HostUrl + $"/{ApiRoutes.ApiPrefix}/RealtimeHub?tenantIdentifier={Uri.EscapeDataString(TenantInfo.Identifier)}", options =>
                {
                    options.AccessTokenProvider = () => GetAccessTokenAsync(connectionToken);
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) })
                .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug); })
                .Build();

            _hubConnection.Closed += exception =>
            {
                _logger.LogDebug("Realtime notification connection closed ({FailureType}).", exception?.GetType().Name ?? "none");
                return Task.CompletedTask;
            };
            _hubConnection.On<string, string>(nameof(SignalRMethods.ReceiveMessage),
                (user, message) => { MessageReceived?.Invoke(user, message); });
            await _hubConnection.StartAsync(cancellation.Token);
            started = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded, disposed, or timed out: notifications must not block the rest of the UI.
        }
        catch (Exception exception)
        {
            // Notifications are optional; stale permissions, offline transport, and token storage
            // failures must not escape through the synchronous LoginChange event into Blazor.
            _logger.LogWarning("Realtime notifications are unavailable ({FailureType}).", exception.GetType().Name);
        }
        finally
        {
            if (acquired)
            {
                if (!started || cancellation.IsCancellationRequested) await DisposeConnectionAsync();
                _connectionLock.Release();
            }
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_initializationCancellation, cancellation)) _initializationCancellation = null;
                cancellation.Dispose();
            }
        }
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken connectionToken)
    {
        // SignalR does not apply its StartAsync token while awaiting this callback. Each acquisition,
        // including an automatic reconnect, therefore owns a deadline linked to the connection.
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(15));
        CancellationToken token = cancellation.Token;
        token.ThrowIfCancellationRequested();
        string? credential = await _localStorageService.GetCachedAuthTokenAsync().AsTask().WaitAsync(token);
        token.ThrowIfCancellationRequested();
        if (AuthTokenService.NeedsRefresh(credential))
        {
            bool refreshed = await _tokenService.RefreshAsync(token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            // Optional notifications cannot log out the app or discard unsaved recovery codes.
            if (!refreshed) return null;
            credential = await _localStorageService.GetCachedAuthTokenAsync().AsTask().WaitAsync(token);
        }
        token.ThrowIfCancellationRequested();
        return credential?.Replace("Bearer ", "").Trim();
    }

    public async Task SendMessageAsync(string user, string message)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync(nameof(SignalRMethods.SendMessage), user, message);
        }
    }

    public bool IsConnected() => _hubConnection?.State == HubConnectionState.Connected;

    public ValueTask DisposeAsync()
    {
        lock (_lifecycleLock)
        {
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        _initializationCancellation?.Cancel();
        _connectionCancellation?.Cancel();
        await _connectionLock.WaitAsync();
        try
        {
            await DisposeConnectionAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task DisposeConnectionAsync()
    {
        HubConnection? connection;
        CancellationTokenSource? cancellation;
        lock (_lifecycleLock)
        {
            connection = _hubConnection;
            _hubConnection = null;
            cancellation = _connectionCancellation;
            _connectionCancellation = null;
            cancellation?.Cancel();
        }
        try
        {
            if (connection is not null) await connection.DisposeAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Unable to cleanly close the realtime notification connection ({FailureType}).", exception.GetType().Name);
        }
        finally
        {
            cancellation?.Dispose();
        }
    }
}
