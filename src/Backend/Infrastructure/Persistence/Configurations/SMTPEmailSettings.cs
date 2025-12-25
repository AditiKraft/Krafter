namespace Backend.Infrastructure.Persistence.Configurations;

public class SMTPEmailSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string SenderEmail { get; set; }
    public string SenderName { get; set; }
}
