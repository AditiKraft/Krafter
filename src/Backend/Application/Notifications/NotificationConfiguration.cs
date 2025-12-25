using System.Net;
using System.Net.Mail;
using Backend.Infrastructure.Persistence.Configurations;
using Backend.Infrastructure.Persistence.Notifications;

namespace Backend.Application.Notifications;

/// <summary>
/// Email/SMTP notification configuration
/// </summary>
public static class NotificationConfiguration
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
