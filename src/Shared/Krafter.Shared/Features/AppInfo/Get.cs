namespace Krafter.Shared.Features.AppInfo;

public sealed class Get
{
    public static class BuildInfo
    {
        public static string DateTimeUtc { get; set; } = "#DateTimeUtc";
        public static string Build { get; set; } = "#Build";
    }
}
