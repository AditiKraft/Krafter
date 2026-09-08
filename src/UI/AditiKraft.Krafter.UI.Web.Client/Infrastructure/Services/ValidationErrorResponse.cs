namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;

public class ValidationErrorResponse
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int Status { get; set; }
    public Dictionary<string, List<string>> Errors { get; set; } = new();
}
