namespace Krafter.UI.Web.Client.Infrastructure.Services;

public class FormFactor : IFormFactor
{
    public string GetFormFactor() => "WebAssembly";

    public string GetPlatform() => Environment.OSVersion.ToString();
}
