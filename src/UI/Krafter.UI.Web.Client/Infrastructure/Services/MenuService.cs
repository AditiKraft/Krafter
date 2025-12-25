using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Permissions;
using Krafter.UI.Web.Client.Models;

namespace Krafter.UI.Web.Client.Infrastructure.Services;

public class MenuService
{
    private Menu[] allMenus = new[]
    {
        new Menu
        {
            Name = "Home",
            Path = "/",
            Icon = "course-icon-static",
            Title = "Manage Courses",
            Description =
                "Manage courses, including creation, updates, and access control for course listings.",
            Tags = new[] { "manage", "courses", "processes", "process" }
        },
        new Menu
        {
            Name = "Account",
            Icon = "account-icon-static",
            Children = new[]
            {
                new Menu
                {
                    Name = "Users",
                    Path = KrafterRoute.Users,
                    Title = "User Management",
                    Description = "Manage user accounts, including creation, updates, and access control.",
                    Icon = "user-icon-static",
                    Tags = new[] { "users", "accounts", "profiles", "authentication" },
                    Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Users)
                },
                new Menu
                {
                    Name = "Roles",
                    Path = KrafterRoute.Roles,
                    Title = "Role Management",
                    Description = "Manage user roles and associated permissions within the system.",
                    Icon = "role-icon-static",
                    Tags = new[] { "roles", "permissions", "access control", "user groups" },
                    Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Roles)
                }
            }
        },
        new Menu
        {
            Name = "Tenants",
            Path = KrafterRoute.Tenants,
            Icon = "tenant-icon-static",
            Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Tenants),
            Title = "Tenant Management",
            Description = "Manage multiple tenants within the system, including creation and updates.",
            Tags = new[] { "tenants", "multi-tenancy", "organizations", "clients" }
        }
    };

    public IEnumerable<Menu> Menus => allMenus;

    public IEnumerable<Menu> Filter(string term)
    {
        if (string.IsNullOrEmpty(term))
        {
            return allMenus;
        }

        bool contains(string value) => value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
        bool filter(Menu menu) => contains(menu.Name) || (menu.Tags != null && menu.Tags.Any(contains));
        bool deepFilter(Menu menu) => filter(menu) || menu.Children?.Any(filter) == true;

        return Menus.Where(category => category.Children?.Any(deepFilter) == true || filter(category))
            .Select(category => new Menu
            {
                Name = category.Name,
                Path = category.Path,
                Icon = category.Icon,
                Permission = category.Permission,
                Expanded = true,
                Children = category.Children?.Where(deepFilter).Select(menu => new Menu
                {
                    Name = menu.Name,
                    Path = menu.Path,
                    Icon = menu.Icon,
                    Expanded = true,
                    Permission = menu.Permission,
                    Children = menu.Children
                }).ToArray()
            }).ToList();
    }

    public Menu FindCurrent(Uri uri)
    {
        IEnumerable<Menu> Flatten(IEnumerable<Menu> e) =>
            e.SelectMany(c => c.Children != null ? Flatten(c.Children) : new[] { c });

        return Flatten(Menus)
            .FirstOrDefault(menu => menu.Path == uri.AbsolutePath || $"/{menu.Path}" == uri.AbsolutePath);
    }

    public string TitleFor(Menu menu)
    {
        if (menu != null && menu.Name != "Overview")
        {
            return menu.Title ?? $"Blazor {menu.Name} Component | Free UI Components by Radzen";
        }

        return "Free Blazor Components | 70+ UI controls by Radzen";
    }

    public string DescriptionFor(Menu menu) => menu?.Description ??
                                               "The Radzen Blazor component library provides more than 70 UI controls for building rich ASP.NET Core web applications.";
}
