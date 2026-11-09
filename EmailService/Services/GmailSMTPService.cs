using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace EmailService.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml, CancellationToken cancellationToken = default);
    }

    public class GmailSmtpService : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<GmailSmtpService> _logger;

        public GmailSmtpService(IConfiguration config, ILogger<GmailSmtpService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml, CancellationToken cancellationToken = default)
        {
            var senderEmail = _config["Smtp:SenderEmail"]
                ?? throw new InvalidOperationException("Missing Smtp:SenderEmail configuration.");
            var senderName = _config["Smtp:SenderName"] ?? "Notification Service";
            var appPassword = _config["Smtp:AppPassword"]
                ?? throw new InvalidOperationException("Missing Smtp:AppPassword configuration.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            if (isHtml)
                bodyBuilder.HtmlBody = body;
            else
                bodyBuilder.TextBody = body;

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // Port 587 with StartTls is open on Azure Functions and required by Gmail
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, cancellationToken);
                await client.AuthenticateAsync(senderEmail, appPassword, cancellationToken);
                await client.SendAsync(message, cancellationToken);

                _logger.LogInformation("Email successfully sent to {Recipient} with subject '{Subject}'.", to, subject);
            }
            finally
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
