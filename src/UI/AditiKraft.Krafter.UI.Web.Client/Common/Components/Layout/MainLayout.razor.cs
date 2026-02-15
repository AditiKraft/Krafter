using AditiKraft.Krafter.UI.Web.Client.Features.Auth._Shared;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Storage;
using AditiKraft.Krafter.UI.Web.Client.Models;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;

public partial class MainLayout(
    ApiCallService api,
    IAppInfoApi appInfoApi,
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

    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

    private RadzenSidebar sidebar0 = default!;
    private RadzenBody body0 = default!;
    private RadzenButton wcagColorsInfo = default!;
    private RadzenButton rtlInfo = default!;
    private RadzenButton freeThemesInfo = default!;
    private RadzenButton premiumThemesInfo = default!;
    private bool sidebarExpanded = true;
    private bool configSidebarExpanded = false;
    private bool rendered;

    private IEnumerable<Menu> menus = new List<Menu>();

    private ICollection<string>? cachedPermissionsAsync = new List<string>();
    public Response<string>? AppInfo { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            rendered = true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        AppInfo = await api.CallAsync(() => appInfoApi.GetAppInfoAsync(), showErrorNotification: false);
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
        string? term = args.Value?.ToString();
        menus = string.IsNullOrEmpty(term) ? menuService.Menus : menuService.Filter(term);
    }

    private void ChangeTheme(object value) => themeService.SetTheme($"{value}");

    private void HeadingChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);

    public void Dispose() => layoutService.HeadingChanged -= HeadingChanged;

    private bool HasChildPermission(Menu category)
    {
        IEnumerable<string> childPermission =
            category.Children?.Select(c => c.Permission) ?? Enumerable.Empty<string>();

        return childPermission.Intersect(cachedPermissionsAsync ?? Enumerable.Empty<string>()).Any();
    }

    private bool HasPermission(Menu category)
    {
        if (string.IsNullOrWhiteSpace(category.Permission) &&
            (category.Children is null || !category.Children.Any()))
        {
            return true;
        }

        return cachedPermissionsAsync?.Contains(category.Permission) == true;
    }
}
