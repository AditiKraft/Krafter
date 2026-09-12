using AditiKraft.Krafter.Contracts.Common;

namespace AditiKraft.Krafter.Backend.Features.Tenants.Common;

internal static class TenantLinkBuilder
{
    internal static string GetTenantLink(AppUrls urls, string? identifier) =>
        urls.GetTenantUiUri(identifier).GetLeftPart(UriPartial.Authority);
}
