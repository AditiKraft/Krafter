namespace AditiKraft.Krafter.Backend.Application.Notifications;

public interface IEmailService
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken);
}

public class SendEmailRequestInput
{
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlMessage { get; set; } = string.Empty;
}
