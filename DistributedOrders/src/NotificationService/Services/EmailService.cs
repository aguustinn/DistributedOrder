namespace NotificationService.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}

// Implementação simulada — em produção, integrar com SendGrid ou Azure Communication Services
public class EmailService(ILogger<EmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string body)
    {
        // Substituir por: await sendGridClient.SendEmailAsync(...) em produção
        logger.LogInformation(
            "📧 [EMAIL SIMULADO] Para: {To} | Assunto: {Subject} | Corpo: {Body}",
            to, subject, body);
        return Task.CompletedTask;
    }
}
