using Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using static Krafter.UI.Web.Client.Infrastructure.Services.ThemeManager;

namespace Krafter.UI.Web.Components;

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
                    theme?.Contains("dark") == true ? ThemePreference.Dark : ThemePreference.Light;
                themeService.SetTheme(theme, false);
            }
            else
            {
                themeManager.CurrentActive = themeService.Theme?.Contains("dark") == true
                    ? ThemePreference.Dark
                    : ThemePreference.Light;
            }
        }
        else
        {
            themeManager.CurrentActive = themeService.Theme?.Contains("dark") == true
                ? ThemePreference.Dark
                : ThemePreference.Light;
        }
    }
}
