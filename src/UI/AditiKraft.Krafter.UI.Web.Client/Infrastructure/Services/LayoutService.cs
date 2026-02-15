namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;

public class LayoutService
{
    public event EventHandler? HeadingChanged;

    public void UpdateHeading(EventArgs eventArgs) => HeadingChanged?.Invoke(this, eventArgs);
}
