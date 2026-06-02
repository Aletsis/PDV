using MediatR;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace PDV.Application.Features.SystemConfiguration.Commands.SendTestEmail;

public record SendTestEmailCommand(
    string SmtpServer,
    int SmtpPort,
    string SmtpUser,
    string SmtpPassword,
    string ToEmail
) : IRequest;

public class SendTestEmailCommandHandler : IRequestHandler<SendTestEmailCommand>
{
    public async Task Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SmtpServer))
            throw new ArgumentException("El servidor SMTP es requerido.");
        if (request.SmtpPort <= 0)
            throw new ArgumentException("El puerto SMTP debe ser un número positivo.");
        if (string.IsNullOrWhiteSpace(request.SmtpUser))
            throw new ArgumentException("El usuario/email SMTP es requerido.");
        if (string.IsNullOrWhiteSpace(request.ToEmail))
            throw new ArgumentException("El email de destino es requerido.");

        using var client = new SmtpClient(request.SmtpServer, request.SmtpPort)
        {
            Credentials = new NetworkCredential(request.SmtpUser, request.SmtpPassword),
            EnableSsl = request.SmtpPort == 587 || request.SmtpPort == 465 || request.SmtpPort == 25 || request.SmtpPort == 465, // Autoenable SSL for common secure ports
            Timeout = 10000 // 10 seconds timeout
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(request.SmtpUser),
            Subject = "Correo de Prueba — POS Antigravity",
            Body = $"<h3>¡Hola!</h3><p>Este es un correo electrónico de prueba enviado desde tu sistema de punto de venta (POS) para verificar que las credenciales SMTP configuradas sean correctas.</p><p>Fecha y Hora: {DateTime.Now:F}</p><br/><p>Saludos,</p><p>Soporte Técnico POS</p>",
            IsBodyHtml = true
        };
        mailMessage.To.Add(request.ToEmail);

        // Send mail asynchronously
        await client.SendMailAsync(mailMessage, cancellationToken);
    }
}
