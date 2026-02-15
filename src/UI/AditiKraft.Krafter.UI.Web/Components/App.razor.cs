namespace AditiKraft.Krafter.UI.Web.Components;

public partial class App(ThemeService themeService, ThemeManager themeManager)
{
    [CascadingParameter] private HttpContext HttpContext { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (HttpContext != null)
        {
            string? theme = HttpContext.Request.Cookies["KrafterTheme"];

            if (!string.IsNullOrEmpty(theme))
            {
                themeManager.CurrentActive =
                    theme?.Contains("dark") == true ? ThemeManager.ThemePreference.Dark : ThemeManager.ThemePreference.Light;
                themeService.SetTheme(theme, false);
            }
            else
            {
                themeManager.CurrentActive = themeService.Theme?.Contains("dark") == true
                    ? ThemeManager.ThemePreference.Dark
                    : ThemeManager.ThemePreference.Light;
            }
        }
        else
        {
            themeManager.CurrentActive = themeService.Theme?.Contains("dark") == true
                ? ThemeManager.ThemePreference.Dark
                : ThemeManager.ThemePreference.Light;
        }
    }
}
