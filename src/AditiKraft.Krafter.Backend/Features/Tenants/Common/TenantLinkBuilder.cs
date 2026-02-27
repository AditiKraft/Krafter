namespace AditiKraft.Krafter.Backend.Features.Tenants.Common;

internal static class TenantLinkBuilder
{
    internal static string GetSubTenantLinkBasedOnRootTenant(string tenantDomain, string? identifier)
    {
        if (tenantDomain.EndsWith("/"))
        {
            tenantDomain = tenantDomain[..^1];
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return tenantDomain;
        }

        if (tenantDomain.Contains("localhost"))
        {
            return tenantDomain;
        }

        string scheme = "";
        string domain = tenantDomain;

        if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "https://";
            domain = domain[8..];
        }
        else if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "http://";
            domain = domain[7..];
        }

        string[] parts = domain.Split('.');
        if (parts.Length > 2)
        {
            parts[0] = identifier;
            domain = string.Join(".", parts);
            return scheme + domain;
        }

        domain = identifier + "." + domain;
        return scheme + domain;
    }
}


