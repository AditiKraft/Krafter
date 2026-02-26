namespace AditiKraft.Krafter.UI.Web.Client.Features.Appearance;

public partial class AppearancePage(
    ThemeService themeService,
#pragma warning disable CS9113 // Parameter is unread.
    CookieThemeService cookieThemeService,
#pragma warning restore CS9113 // Parameter is unread.
    ThemeManager themeManager
) : ComponentBase
{
    private async Task ChangeThemeAsync(string value) => await themeManager.SetDifferentThemeAsync(value);

    private void ChangeRightToLeft(bool value) => themeService.SetRightToLeft(value);

    private void ChangeWcag(bool value) => themeService.SetWcag(value);
}
