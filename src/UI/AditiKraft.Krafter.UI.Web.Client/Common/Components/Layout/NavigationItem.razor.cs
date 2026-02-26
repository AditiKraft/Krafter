using AditiKraft.Krafter.UI.Web.Client.Models;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;

public partial class NavigationItem()
{
    [EditorRequired]
    [Parameter]
    public Menu Example { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<bool> ExpandedChanged { get; set; }

    [Parameter] public bool Expanded { get; set; }

    protected override void OnParametersSet()
    {
        Example?.Expanded = Expanded;
    }

    private string GetUrl()
    {
        //  return Example.Path == null ? Example.Path : $"{Example.Path}{new Uri(navigationManager.Uri).Query}";
        return Example.Path;
    }
}
