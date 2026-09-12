namespace AditiKraft.Krafter.Backend.Infrastructure.Notifications;

public class SendEmailRequestInput
{
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlMessage { get; set; } = string.Empty;
}
