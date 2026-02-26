namespace AditiKraft.Krafter.UI.Web.Client.Common.Models;

public static class LocalAppSate
{
    public static string CurrentPageTitle { get; set; } = "Home"; //Density

    public static Density Density { get; set; } = Density.Compact;
    public static bool AllowColumnResize { get; set; } = true;
    public static bool ShowCellDataAsTooltip { get; set; } = true;
    public static string DateFormat { get; set; } = "dd/MM/yyyy";

    //12 hour date time format
    public static string DateTimeFormat { get; set; } = "dd/MM/yyyy hh:mm tt";

    public static string TimeFormat { get; set; } = "hh:mm tt";

    public static string GoogleLoginReturnUrl { get; set; } = string.Empty;
    //
}

public static class TenantInfo
{
    public static string Identifier { get; set; } = string.Empty;
    public static string HostUrl { get; set; } = string.Empty;
    public static string MainDomain { get; set; } = "aka.gdn";
}
