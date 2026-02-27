using System.Net.Mail;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Configurations;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence.Notifications;

public class EmailService(SmtpClient smtpClient, SMTPEmailSettings smtpEmailSettings) : IEmailService
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage();
        message.From = new MailAddress(smtpEmailSettings.SenderEmail, smtpEmailSettings.SenderName);
        message.To.Add(new MailAddress(email));
        message.Subject = subject;
        message.Body = htmlMessage;
        message.IsBodyHtml = true;
        await smtpClient.SendMailAsync(message);
    }
}


