using System.Net;
using System.Net.Mail;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Configurations;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Notifications;

namespace AditiKraft.Krafter.Backend.Notifications;

public static class NotificationsConfiguration
{
    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        SMTPEmailSettings smtpSettings = configuration.GetSection("SMTPEmailSettings").Get<SMTPEmailSettings>()
                                         ?? throw new InvalidOperationException("SMTP settings must be configured");

        services.AddSingleton(smtpSettings);

        services.AddSingleton<SmtpClient>(sp =>
        {
            SMTPEmailSettings settings = sp.GetRequiredService<SMTPEmailSettings>();
            return new SmtpClient(settings.Host, settings.Port)
            {
                Credentials = new NetworkCredential(settings.UserName, settings.Password), EnableSsl = true
            };
        });

        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}

