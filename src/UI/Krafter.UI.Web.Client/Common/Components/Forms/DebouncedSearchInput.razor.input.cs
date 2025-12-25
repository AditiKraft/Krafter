namespace Krafter.UI.Web.Client.Common.Components.Forms;

public class DebouncedSearchInputInput
{
    public string Value { get; set; }
    public EventCallback<string> ValueChanged { get; set; }
    public int DebounceTime { get; set; } = 2000;
    [Parameter] public bool IsAutoSearch { get; set; } = true;

    //Add property to for auto search or not so that we can show button to click or not
}
