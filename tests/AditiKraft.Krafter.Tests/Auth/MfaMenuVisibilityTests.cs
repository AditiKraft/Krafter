using AditiKraft.Krafter.UI.Web.Client.Common.Components.Layout;
using AditiKraft.Krafter.UI.Web.Client.Common.Models;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class MfaMenuVisibilityTests
{
    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, "two-factor")]
    [InlineData(true, "security")]
    [InlineData(true, "authenticator")]
    public async Task SelfServiceSecurityRemainsVisibleWithoutAdministrativePermissions(bool basicRole, string search)
    {
        var menus = new MenuService();
        string[] permissions = basicRole ? ["Permissions.Notifications.View"] : [];
        string html = await RenderAsync(string.IsNullOrEmpty(search) ? menus.Menus : menus.Filter(search), permissions);

        Assert.Contains("Two-factor authentication", html);
        Assert.Contains("account/two-factor", html);
        Assert.DoesNotContain("aria-label=\"Users\"", html);
        Assert.DoesNotContain("aria-label=\"Roles\"", html);
    }

    [Theory]
    [InlineData("two-factor")]
    [InlineData("security")]
    [InlineData("authenticator")]
    public async Task FilteringAnAdministratorMenuToItsUnrestrictedChildKeepsTheAccountGroup(string search)
    {
        string html = await RenderAsync(new MenuService().Filter(search), ["Permissions.Users.View", "Permissions.Roles.View"]);

        Assert.Contains("aria-label=\"Account\"", html);
        Assert.Contains("account/two-factor", html);
    }

    [Fact]
    public async Task AdministratorStillSeesPermissionedAccountPages()
    {
        string html = await RenderAsync(new MenuService().Menus, ["Permissions.Users.View", "Permissions.Roles.View"]);

        Assert.Contains("aria-label=\"Users\"", html);
        Assert.Contains("aria-label=\"Roles\"", html);
    }

    [Fact]
    public async Task NestedGroupsUseTheSameLeafAndGroupPermissionRulesAtEveryDepth()
    {
        Menu[] menus =
        [
            new()
            {
                Name = "Outer", Children =
                [
                    new()
                    {
                        Name = "Middle", Children =
                        [
                            new()
                            {
                                Name = "Inner", Children =
                                [
                                    new() { Name = "Self-service", Path = "account/two-factor" },
                                    new() { Name = "Restricted leaf", Path = "restricted", Permission = "missing" }
                                ]
                            }
                        ]
                    },
                    new()
                    {
                        Name = "Restricted group", Permission = "missing", Children =
                        [new() { Name = "Unrestricted child of restricted group", Path = "private-child" }]
                    },
                    new()
                    {
                        Name = "Inaccessible group", Children =
                        [new() { Name = "Other restricted leaf", Path = "other-private", Permission = "missing" }]
                    }
                ]
            }
        ];

        string html = await RenderAsync(menus, []);
        Assert.Contains("aria-label=\"Outer\"", html);
        Assert.Contains("aria-label=\"Middle\"", html);
        Assert.Contains("aria-label=\"Inner\"", html);
        Assert.Contains("account/two-factor", html);
        Assert.DoesNotContain("Restricted leaf", html);
        Assert.DoesNotContain("Restricted group", html);
        Assert.DoesNotContain("private-child", html);
        Assert.DoesNotContain("Inaccessible group", html);
    }

    private static async Task<string> RenderAsync(IEnumerable<Menu> menus, ICollection<string> permissions)
    {
        Menu[] items = menus.ToArray();
        Expand(items);
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton<NavigationManager, TestNavigation>();
        registrations.AddSingleton<IJSRuntime, NoJavaScript>();
        await using ServiceProvider services = registrations.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment menuItems = builder =>
            {
                builder.OpenComponent<NavigationMenuItems>(0);
                builder.AddAttribute(1, nameof(NavigationMenuItems.Items), items);
                builder.AddAttribute(2, nameof(NavigationMenuItems.Permissions), permissions);
                builder.CloseComponent();
            };
            // Match MainLayout: Radzen menu items require the panel's cascading navigation state.
            var root = await renderer.RenderComponentAsync<RadzenPanelMenu>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(RadzenPanelMenu.ChildContent)] = menuItems
                }));
            return root.ToHtmlString();
        });
    }

    private static void Expand(IEnumerable<Menu> menus)
    {
        foreach (Menu menu in menus)
        {
            menu.Expanded = true;
            if (menu.Children is not null) Expand(menu.Children);
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://app.example/", "https://app.example/");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
