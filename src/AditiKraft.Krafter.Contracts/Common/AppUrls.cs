using System.Text.Json.Serialization;

namespace AditiKraft.Krafter.Contracts.Common;

public sealed class AppUrls
{
    public const string SectionName = "Urls";
    public const string ConfigurationPath = "configuration/urls";

    public string RootUiUrl { get; set; } = default!;
    public string? ApiBaseUrl { get; set; }
    public string? TenantBaseDomain { get; set; }
    public string[] ReservedTenantIdentifiers { get; set; } = [];

    // Internal connection addresses must never be sent to the browser.
    [JsonIgnore]
    public string? ServerApiBaseUrl { get; set; }

    public Uri GetRootUiUri() => ParseOrigin(RootUiUrl, "Urls:RootUiUrl");

    public Uri? GetApiUri() => ApiBaseUrl is null ? null : ParseOrigin(ApiBaseUrl, "Urls:ApiBaseUrl");

    public Uri GetServerApiUri() => ServerApiBaseUrl is null
        ? GetApiUri() ?? GetRootUiUri()
        : ParseOrigin(ServerApiBaseUrl, "Urls:ServerApiBaseUrl");

    public Uri GetGoogleRedirectUri() => new(GetRootUiUri(), "google-callback");

    public Uri GetTenantBaseUri()
    {
        Uri root = GetRootUiUri();
        if (string.IsNullOrEmpty(TenantBaseDomain))
        {
            return root;
        }

        string domain = TenantBaseDomain;
        if (domain.Contains("://", StringComparison.Ordinal))
        {
            Uri origin = ParseOrigin(domain, "Urls:TenantBaseDomain");
            if (origin.Scheme != root.Scheme || origin.Port != root.Port)
            {
                throw new InvalidOperationException(
                    "Configuration 'Urls:TenantBaseDomain' must use the same scheme and port as 'Urls:RootUiUrl'. Use a bare domain to inherit them automatically.");
            }

            domain = origin.Host;
        }

        if (domain.Length > 253 || !domain.Contains('.') ||
            domain.Split('.').Any(label => !IsDnsLabel(label)) ||
            Uri.CheckHostName(domain) != UriHostNameType.Dns ||
            domain.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Configuration 'Urls:TenantBaseDomain' must be a DNS domain such as example.com or an HTTP/HTTPS origin such as https://example.com, without a path, query, fragment, or credentials. Leave it empty for local development.");
        }

        Uri tenantBase = new UriBuilder(root) { Host = domain }.Uri;
        if (!root.Host.Equals(tenantBase.Host, StringComparison.OrdinalIgnoreCase) &&
            GetSubdomain(root.Host, tenantBase) is null)
        {
            throw new InvalidOperationException(
                "Configuration 'Urls:RootUiUrl' must use 'Urls:TenantBaseDomain' or one subdomain directly below it.");
        }

        return tenantBase;
    }

    public string? GetUiTenantIdentifier(string host) =>
        host.Equals(GetRootUiUri().Host, StringComparison.OrdinalIgnoreCase)
            ? null
            : GetSubdomain(host, GetTenantBaseUri());

    public bool IsInvalidUiTenantHost(string host)
    {
        if (host.Equals(GetRootUiUri().Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? identifier = GetUiTenantIdentifier(host);
        return identifier is not null
            ? IsReservedTenantIdentifier(identifier)
            : host.EndsWith("." + GetTenantBaseUri().Host, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsReservedTenantIdentifier(string identifier)
    {
        if (new[] { DefaultTenantConstants.Identifier, "api", "www", "mail" }
                .Contains(identifier, StringComparer.OrdinalIgnoreCase) ||
            ReservedTenantIdentifiers.Contains(identifier, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // A validator can also be used without host configuration, for basic contract checks.
        if (string.IsNullOrEmpty(RootUiUrl))
        {
            return false;
        }

        Uri tenantBase = GetTenantBaseUri();
        return new[] { GetRootUiUri(), GetApiUri(), GetServerApiUri() }
            .Any(uri => uri is not null && string.Equals(
                GetSubdomain(uri.Host, tenantBase), identifier, StringComparison.OrdinalIgnoreCase));
    }

    public Uri GetTenantUiUri(string? identifier)
    {
        Uri root = GetRootUiUri();
        Uri tenantBase = GetTenantBaseUri();
        if (string.IsNullOrEmpty(identifier) ||
            identifier.Equals(DefaultTenantConstants.Identifier, StringComparison.OrdinalIgnoreCase) ||
            !SupportsTenantSubdomains(tenantBase))
        {
            return root;
        }

        if (!IsDnsLabel(identifier) || IsReservedTenantIdentifier(identifier))
        {
            throw new InvalidOperationException("The tenant identifier cannot be used in a tenant URL.");
        }

        return new UriBuilder(tenantBase) { Host = $"{identifier.ToLowerInvariant()}.{tenantBase.Host}" }.Uri;
    }

    public bool CanUseTenantIdentifier(string? identifier) =>
        identifier is not null && IsDnsLabel(identifier) &&
        (identifier.Equals(DefaultTenantConstants.Identifier, StringComparison.OrdinalIgnoreCase) ||
         !IsReservedTenantIdentifier(identifier));

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
        if (Uri.CheckHostName(host) != UriHostNameType.Dns || !SupportsTenantSubdomains(rootUri))
        {
            return null;
        }

        string suffix = "." + rootUri.Host;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string subdomain = host[..^suffix.Length];
        return IsDnsLabel(subdomain) ? subdomain.ToLowerInvariant() : null;
    }

    private static bool IsDnsLabel(string value) => value.Length is > 0 and <= 63
        && char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1])
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static bool SupportsTenantSubdomains(Uri uri) => uri.HostNameType == UriHostNameType.Dns
        && uri.Host.Contains('.') && !uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
}
