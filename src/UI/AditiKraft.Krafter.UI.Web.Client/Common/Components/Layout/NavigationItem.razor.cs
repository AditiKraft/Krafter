using AditiKraft.Krafter.UI.Web.Client.Models;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;

public partial class NavigationItem(NavigationManager navigationManager)
{
    [Parameter] public Menu Example { get; set; }

    [Parameter] public RenderFragment ChildContent { get; set; }
    [Parameter] public EventCallback<bool> ExpandedChanged { get; set; }

    [Parameter]
    public bool Expanded
    {
        get => Example.Expanded;
        set => Example.Expanded = value;
    }

    private string GetUrl()
    {
        //  return Example.Path == null ? Example.Path : $"{Example.Path}{new Uri(navigationManager.Uri).Query}";
        return Example.Path;
    }
}
