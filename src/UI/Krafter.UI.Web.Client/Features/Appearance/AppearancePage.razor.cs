using Krafter.UI.Web.Client.Infrastructure.Services;
using static Krafter.UI.Web.Client.Infrastructure.Services.ThemeManager;

namespace Krafter.UI.Web.Client.Features.Appearance;

public partial class AppearancePage(
    ThemeService themeService,
    CookieThemeService cookieThemeService,
    ThemeManager themeManager
) : ComponentBase
{
    private void ChangeTheme(string value) => themeManager.SetDifferentTheme(value);

    private void ChangeRightToLeft(bool value) => themeService.SetRightToLeft(value);

    private void ChangeWcag(bool value) => themeService.SetWcag(value);
}
