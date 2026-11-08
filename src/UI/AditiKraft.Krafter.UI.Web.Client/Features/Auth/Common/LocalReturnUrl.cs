namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;

public static class LocalReturnUrl
{
    public static string Normalize(string? value) =>
        AditiKraft.Krafter.Contracts.Common.Auth.LocalReturnPath.Normalize(value);
}
