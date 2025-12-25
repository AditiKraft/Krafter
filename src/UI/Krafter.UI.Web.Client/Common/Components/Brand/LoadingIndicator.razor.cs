namespace Krafter.UI.Web.Client.Common.Components.Brand;

public partial class LoadingIndicator
{
    [Parameter] public LoadingIndicatorSize Size { get; set; } = LoadingIndicatorSize.Medium;

    public string GetAlt()
    {
        if (Size == LoadingIndicatorSize.ExtraSmall)
        {
            return "extra small logo";
        }
        else if (Size == LoadingIndicatorSize.Small)
        {
            return "small logo";
        }
        else if (Size == LoadingIndicatorSize.Medium)
        {
            return "medium logo";
        }
        else if (Size == LoadingIndicatorSize.Large)
        {
            return "large logo";
        }
        else if (Size == LoadingIndicatorSize.ExtraLarge)
        {
            return "extra large logo";
        }
        else
        {
            return "medium logo";
        }
    }

    private string GetSource()
    {
        if (Size == LoadingIndicatorSize.ExtraSmall)
        {
            return "brand/loading-indicators/loading-indicator-xs.svg";
        }
        else if (Size == LoadingIndicatorSize.Small)
        {
            return "brand/loading-indicators/loading-indicator-s.svg";
        }
        else if (Size == LoadingIndicatorSize.Medium)
        {
            return "brand/loading-indicators/loading-indicator-m.svg";
        }
        else if (Size == LoadingIndicatorSize.Large)
        {
            return "brand/loading-indicators/loading-indicator-l.svg";
        }
        else if (Size == LoadingIndicatorSize.ExtraLarge)
        {
            return "brand/loading-indicators/loading-indicator-xl.svg";
        }
        else
        {
            return "brand/loading-indicators/loading-indicator-m.svg";
        }
    }
}

public enum LoadingIndicatorSize
{
    ExtraSmall = 0,
    Small = 1,
    Medium = 2,
    Large = 3,
    ExtraLarge = 4
}
