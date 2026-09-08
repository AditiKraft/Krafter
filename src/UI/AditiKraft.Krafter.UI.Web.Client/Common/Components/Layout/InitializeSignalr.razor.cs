using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;

public partial class InitializeSignalr(
    IAuthenticationService authenticationService,
    SignalRService signalRService
)
{
    protected override async Task OnInitializedAsync()
    {
        authenticationService.LoginChange += async name => { await signalRService.InitializeAsync(); };
        await signalRService.InitializeAsync();
    }
}
