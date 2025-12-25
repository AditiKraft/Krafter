using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Features.Auth._Shared;
using Krafter.UI.Web.Client.Models;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Krafter.UI.Web.Client.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;

namespace Krafter.UI.Web.Client.Common.Components.Layout;

public partial class MainLayout(
    KrafterClient krafterClient,
    CookieThemeService CookieThemeService,
    MenuService menuService,
    LayoutService layoutService,
    IAuthenticationService authenticationService,
    ThemeService themeService,
    TooltipService tooltipService,
    ThemeManager themeManager,
    IKrafterLocalStorageService krafterLocalStorageService
) : IDisposable
{
    [CascadingParameter] public bool IsMobileDevice { get; set; }

    [CascadingParameter] private HttpContext HttpContext { get; set; }

    private RadzenSidebar sidebar0;
    private RadzenBody body0;
    private RadzenButton wcagColorsInfo;
    private RadzenButton rtlInfo;
    private RadzenButton freeThemesInfo;
    private RadzenButton premiumThemesInfo;
    private bool sidebarExpanded = true;
    private bool configSidebarExpanded = false;
    private bool rendered;

    private IEnumerable<Menu> menus = new List<Menu>();

    private ICollection<string>? cachedPermissionsAsync = new List<string>();
    public StringResponse? AppInfo { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            rendered = true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        AppInfo = await krafterClient.AppInfo.GetAsync();
        cachedPermissionsAsync = await krafterLocalStorageService.GetCachedPermissionsAsync();
        if (cachedPermissionsAsync is null)
        {
            cachedPermissionsAsync = new List<string>();
        }

        menus = menuService.Menus;
        layoutService.HeadingChanged += HeadingChanged;
        authenticationService.LoginChange += async name =>
        {
            cachedPermissionsAsync = await krafterLocalStorageService.GetCachedPermissionsAsync();
            if (cachedPermissionsAsync is null)
            {
                cachedPermissionsAsync = new List<string>();
            }

            menus = menuService.Menus;
        };
    }

    private void FilterPanelMenu(ChangeEventArgs args)
    {
        string? term = args.Value.ToString();
        menus = string.IsNullOrEmpty(term) ? menuService.Menus : menuService.Filter(term);
    }

    private void ChangeTheme(object value) => themeService.SetTheme($"{value}");

    private void HeadingChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);

    public void Dispose() => layoutService.HeadingChanged -= HeadingChanged;

    private bool HasChildPermission(Menu category)
    {
        IEnumerable<string> childPermission = category.Children.Select(c => c.Permission);

        return childPermission.Intersect(cachedPermissionsAsync).Count() > 0;
    }

    private bool HasPermission(Menu category)
    {
        if (string.IsNullOrWhiteSpace(category.Permission) &&
            (category.Children is null || category.Children.Count() == 0))
        {
            return true;
        }

        return cachedPermissionsAsync.Contains(category.Permission);
    }
}
