using Krafter.UI.Web.Client.Infrastructure.SignalR;
using Radzen.Blazor.Rendering;

namespace Krafter.UI.Web.Client.Common.Components.Layout;

public partial class Notifications(SignalRService signalRService) : IDisposable
{
    private RadzenButton button;
    private Popup popup;
    private List<string> messages = new();

    protected override async Task OnInitializedAsync() => signalRService.MessageReceived += OnMessageReceived;

    private async Task OnOpen()
    {
        // await JSRuntime.InvokeVoidAsync("eval", "setTimeout(function(){ document.getElementById('search').focus(); }, 200)");
    }

    private void OnMessageReceived(string user, string message)
    {
        string encodedMsg = $"{user}: {message}";
        messages.Add(encodedMsg);
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() => signalRService.MessageReceived -= OnMessageReceived;
}
