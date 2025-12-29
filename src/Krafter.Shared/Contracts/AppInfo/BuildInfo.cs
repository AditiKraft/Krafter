namespace Krafter.Shared.Contracts.AppInfo;

public static class BuildInfo
{
    public static string Build { get; } = "1.0.0";
    public static string DateTimeUtc { get; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
