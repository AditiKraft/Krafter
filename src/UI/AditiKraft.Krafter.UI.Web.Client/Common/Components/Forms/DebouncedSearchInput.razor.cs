namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Forms;

public partial class DebouncedSearchInput
{
    [Parameter] public string Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public int DebounceTime { get; set; } = 2000;
    [Parameter] public bool IsAutoSearch { get; set; } = true;
    [Parameter] public bool IsLoading { get; set; } = false;

    private Timer debounceTimer;

    private void OnInputChanged(ChangeEventArgs e)
    {
        Value = e.Value.ToString();
        if (IsAutoSearch)
        {
            DebounceSearch();
        }
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && IsAutoSearch == false)
        {
            TriggerSearch();
        }
    }

    private void DebounceSearch()
    {
        if (debounceTimer != null)
        {
            debounceTimer.Dispose();
        }

        debounceTimer = new Timer(async _ => { await InvokeAsync(() => ValueChanged.InvokeAsync(Value)); }, null,
            DebounceTime, Timeout.Infinite);
    }

    private async Task TriggerSearch() => await ValueChanged.InvokeAsync(Value);
}
