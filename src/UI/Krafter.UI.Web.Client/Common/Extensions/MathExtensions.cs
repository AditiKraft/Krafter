namespace Krafter.UI.Web.Client.Common.Extensions;

public static class MathExtensions
{
    public static double? SafeRound(this double? value, int digits = 2) =>
        value.HasValue ? Math.Round(value.Value, digits) : (double?)null;

    public static decimal? SafeRound(this decimal? value, int digits = 2) =>
        value.HasValue ? Math.Round(value.Value, digits) : (decimal?)null;

    public static int? SafeRound(this int? value, int digits = 2) =>
        value.HasValue ? (int?)Math.Round((double)value.Value, digits) : (int?)null;

    public static double SafeRound(this double value, int digits = 2) => Math.Round(value, digits);

    public static decimal SafeRound(this decimal value, int digits = 2) => Math.Round(value, digits);

    public static int SafeRound(this int value, int digits = 2) => (int)Math.Round((double)value, digits);
}
