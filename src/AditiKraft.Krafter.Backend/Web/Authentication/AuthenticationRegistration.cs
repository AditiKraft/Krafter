using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Backend.Web.Configuration;
using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Contracts.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Backend.Web.Authentication;

public static class AuthenticationRegistration
{
    internal static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JwtSettings>()
            .BindConfiguration($"SecuritySettings:{nameof(JwtSettings)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        return services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddGoogle(options =>
            {
                options.ClientId = config["Authentication:Google:ClientId"] ?? "";
                options.ClientSecret = config["Authentication:Google:ClientSecret"] ?? "";
                options.CallbackPath = "/signin-google";
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, null!)
            .Services;
    }

    public static IServiceCollection AddCurrentUserServices(this IServiceCollection services)
    {
        services
            .AddScoped<CurrentUserMiddleware>()
            .AddScoped<ICurrentUser, CurrentUser>()
            .AddScoped(sp => (ICurrentUserInitializer)sp.GetRequiredService<ICurrentUser>());

        return services;
    }

    public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddCurrentUserServices()
            .AddPermissions()
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.Configure<SecuritySettings>(config.GetSection(nameof(SecuritySettings)));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<RolePermissionService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddHttpClient<ExternalAuth.GoogleAuthClient>(client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services.AddJwtAuth(config);
    }

    private static IServiceCollection AddPermissions(this IServiceCollection services)
    {
        return services
            .AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
            .AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    }
}
