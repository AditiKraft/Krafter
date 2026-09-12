using System.Text.Json.Serialization;

namespace AditiKraft.Krafter.Contracts.Common;

public sealed class AppUrls
{
    public const string SectionName = "Urls";
    public const string ConfigurationPath = "configuration/urls";

    public string RootUiUrl { get; set; } = default!;
    public string? ApiBaseUrl { get; set; }

    // Internal connection addresses must never be sent to the browser.
    [JsonIgnore]
    public string? ServerApiBaseUrl { get; set; }

    public Uri GetRootUiUri() => ParseOrigin(RootUiUrl, "Urls:RootUiUrl");

    public Uri? GetApiUri() => ApiBaseUrl is null ? null : ParseOrigin(ApiBaseUrl, "Urls:ApiBaseUrl");

    public Uri GetServerApiUri() => ServerApiBaseUrl is null
        ? GetApiUri() ?? GetRootUiUri()
        : ParseOrigin(ServerApiBaseUrl, "Urls:ServerApiBaseUrl");

    public Uri GetGoogleRedirectUri() => new(GetRootUiUri(), "google-callback");

    public static Uri ParseOrigin(string? value, string configurationKey)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host)
            || uri.UserInfo.Length != 0
            || uri.AbsolutePath != "/"
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{configurationKey}' must be an absolute HTTP or HTTPS origin, such as https://app.example.com, without a path, query, fragment, or credentials.");
        }

        return uri;
    }

    public static string? GetSubdomain(string host, Uri rootUri)
    {
        if (Uri.CheckHostName(host) != UriHostNameType.Dns
            || rootUri.HostNameType != UriHostNameType.Dns
            || !rootUri.Host.Contains('.')
            || rootUri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string suffix = "." + rootUri.Host;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string subdomain = host[..^suffix.Length];
        return subdomain.Length is > 0 and <= 63
            && char.IsAsciiLetterOrDigit(subdomain[0]) && char.IsAsciiLetterOrDigit(subdomain[^1])
            && subdomain.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
                ? subdomain.ToLowerInvariant()
                : null;
    }
}
