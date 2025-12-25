using System.Net.Mail;
using Backend.Application.Notifications;
using Backend.Infrastructure.Persistence.Configurations;

namespace Backend.Infrastructure.Persistence.Notifications;

public class EmailService(SmtpClient smtpClient, SMTPEmailSettings smtpEmailSettings) : IEmailService
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
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
