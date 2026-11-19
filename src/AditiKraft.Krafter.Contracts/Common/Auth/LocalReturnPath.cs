namespace AditiKraft.Krafter.Contracts.Common.Auth;

public static class LocalReturnPath
{
    public static string Normalize(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith('/') &&
        !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') &&
        !value.Any(char.IsControl) &&
        !value.StartsWith("/login", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("/google-", StringComparison.OrdinalIgnoreCase) ? value : "/";
}
