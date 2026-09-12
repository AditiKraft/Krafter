using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;

public static class UiUrlConfiguration
{
    public static AppUrls Resolve(IConfiguration configuration, BlazorHostingMode hostingMode)
    {
        AppUrls urls = configuration.GetSection(AppUrls.SectionName).Get<AppUrls>() ?? new AppUrls();
        _ = urls.GetRootUiUri();

        if (hostingMode == BlazorHostingMode.SingleHost)
        {
            if (urls.ApiBaseUrl is not null)
            {
                throw new InvalidOperationException(
                    "Remove 'Urls:ApiBaseUrl' in single-host mode. Browser API calls use the current origin.");
            }
        }
        else
        {
            _ = urls.GetApiUri() ?? throw new InvalidOperationException(
                "Configuration 'Urls:ApiBaseUrl' is required in split-host mode. Set the public API origin.");
            urls.ServerApiBaseUrl ??= configuration["services:api:https:0"]
                ?? configuration["services:api:http:0"];
        }

        _ = urls.GetServerApiUri();
        return urls;
    }

    public static IEndpointConventionBuilder MapUiUrlConfiguration(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/" + AppUrls.ConfigurationPath, (AppUrls urls, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(new AppUrls { RootUiUrl = urls.RootUiUrl, ApiBaseUrl = urls.ApiBaseUrl });
        }).AllowAnonymous();
    }
}
