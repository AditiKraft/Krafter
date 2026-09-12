using AditiKraft.Krafter.Contracts.Common;

namespace AditiKraft.Krafter.Backend.Web.Configuration;

public static class CorsConfiguration
{
    private const string PolicyName = "AllowSpecificOrigins";

    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AppUrls urls = configuration.GetSection(AppUrls.SectionName).Get<AppUrls>() ?? new AppUrls();
        Uri rootUiOrigin = urls.GetRootUiUri();
        string[] additionalOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        Uri[] allowedOrigins = additionalOrigins
            .Select((origin, index) => AppUrls.ParseOrigin(origin, $"Cors:AllowedOrigins:{index}"))
            .Prepend(rootUiOrigin)
            .ToArray();
        bool allowTenantSubdomains = configuration.GetValue<bool>("Cors:AllowTenantSubdomains");

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .SetIsOriginAllowed(origin => IsOriginAllowed(origin, rootUiOrigin, allowedOrigins, allowTenantSubdomains))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }

    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app) => app.UseCors(PolicyName);

    private static bool IsOriginAllowed(
        string origin,
        Uri rootUiOrigin,
        Uri[] allowedOrigins,
        bool allowTenantSubdomains)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" ||
            uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return false;
        }

        if (allowedOrigins.Any(allowed => HasSameOrigin(uri, allowed)))
        {
            return true;
        }

        if (!allowTenantSubdomains || rootUiOrigin.HostNameType != UriHostNameType.Dns ||
            rootUiOrigin.IsLoopback || !rootUiOrigin.IdnHost.Contains('.') ||
            uri.HostNameType != UriHostNameType.Dns ||
            uri.Scheme != rootUiOrigin.Scheme || uri.Port != rootUiOrigin.Port)
        {
            return false;
        }

        return AppUrls.GetSubdomain(uri.IdnHost, rootUiOrigin) is not null;
    }

    private static bool HasSameOrigin(Uri origin, Uri allowed) =>
        origin.Scheme == allowed.Scheme && origin.Port == allowed.Port &&
        string.Equals(origin.IdnHost, allowed.IdnHost, StringComparison.OrdinalIgnoreCase);
}
