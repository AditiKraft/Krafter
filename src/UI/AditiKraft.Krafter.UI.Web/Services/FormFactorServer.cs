using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;

namespace AditiKraft.Krafter.UI.Web.Services;

public class FormFactorServer : IFormFactor
{
    public string GetFormFactor() => "Web";

    public string GetPlatform() => Environment.OSVersion.ToString();
}
