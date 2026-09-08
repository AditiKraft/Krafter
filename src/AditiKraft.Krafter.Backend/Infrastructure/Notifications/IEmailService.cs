namespace AditiKraft.Krafter.Backend.Infrastructure.Notifications;

public interface IEmailService
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken);
}
