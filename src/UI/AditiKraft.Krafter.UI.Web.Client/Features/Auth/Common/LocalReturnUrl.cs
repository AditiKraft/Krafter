namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;

public static class LocalReturnUrl
{
    public static string Normalize(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith('/') &&
        !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') &&
        // Browsers strip raw tabs/newlines before parsing, which can turn /<tab>/ into //.
        !value.Any(char.IsControl) &&
        !value.StartsWith("/login", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase) ? value : "/";
}
