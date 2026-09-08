using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.SignalR;
using Blazored.SessionStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AditiKraft.Krafter.UI.Web.Client;

public static class RegisterUIServices
{
    public static void AddUIServices(this IServiceCollection service)
    {
        service.AddRadzenCookieThemeService(options =>
        {
            options.Name = "AppTheme"; // The name of the cookie
            options.Duration = TimeSpan.FromDays(365); // The duration of the cookie
        });

        service.AddScoped<ThemeManager>();
        service.AddScoped<SignalRService>();
        service.AddBlazoredSessionStorage();
        service.AddScoped<MenuService>();
        service.AddScoped<LayoutService>();

        service.AddScoped<IAuthenticationService, AuthenticationService>();
        service.AddScoped<AuthTokenService>();
        service.TryAddScoped<TokenRefreshCoordinator>();
        service.AddScoped<NotificationService>();
        service.AddScoped<ApiCallService>();
    }
}
