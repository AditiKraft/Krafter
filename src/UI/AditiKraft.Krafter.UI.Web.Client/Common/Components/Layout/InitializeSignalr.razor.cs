using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;

public partial class InitializeSignalr(
    IAuthenticationService authenticationService,
    SignalRService signalRService
) : IDisposable
{
    protected override async Task OnInitializedAsync()
    {
        authenticationService.LoginChange += OnLoginChanged;
        await signalRService.InitializeAsync();
    }

    // InitializeAsync contains connection failures, so this Action event needs no async-void handler.
    private void OnLoginChanged(string? name) => _ = signalRService.InitializeAsync();

    public void Dispose() => authenticationService.LoginChange -= OnLoginChanged;
}
