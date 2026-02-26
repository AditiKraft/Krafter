namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Forms;

public partial class DebouncedSearchInput
{
    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public int DebounceTime { get; set; } = 2000;
    [Parameter] public bool IsAutoSearch { get; set; } = true;
    [Parameter] public bool IsLoading { get; set; } = false;

    private Timer? _debounceTimer;

    private void OnInputChanged(ChangeEventArgs e)
    {
        Value = e.Value?.ToString() ?? string.Empty;
        if (IsAutoSearch)
        {
            DebounceSearch();
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && IsAutoSearch == false)
        {
            await TriggerSearchAsync();
        }
    }

    private void DebounceSearch()
    {
        _debounceTimer?.Dispose();

        _debounceTimer = new Timer(async _ => { await InvokeAsync(() => ValueChanged.InvokeAsync(Value)); }, null,
            DebounceTime, Timeout.Infinite);
    }

    private async Task TriggerSearchAsync() => await ValueChanged.InvokeAsync(Value);
}
