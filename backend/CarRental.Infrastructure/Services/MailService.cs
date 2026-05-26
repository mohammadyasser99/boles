using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;

namespace CarRental.Infrastructure.Services
{
    public class MailService : IMailManager
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MailService> _logger;

        public MailService(IConfiguration config, ILogger<MailService> logger)
        {
            _config = config;
            _logger = logger;

            _logger.LogInformation("MailService initialized");

            // Validate mail configuration on startup
            ValidateMailConfiguration();
        }

        public bool SendMail(MailData mailData)
        {
            if (mailData == null)
            {
                _logger.LogError("SendMail failed: MailData is null");
                throw new ArgumentNullException(nameof(mailData), "Mail data cannot be null");
            }

            if (string.IsNullOrEmpty(mailData.RecieverMail))
            {
                _logger.LogError("SendMail failed: Recipient email is null or empty");
                throw new ArgumentException("Recipient email cannot be null or empty", nameof(mailData.RecieverMail));
            }

            if (string.IsNullOrEmpty(mailData.EmailSubject))
            {
                _logger.LogWarning("SendMail called with empty subject for recipient: {Recipient}", mailData.RecieverMail);
            }

            if (string.IsNullOrEmpty(mailData.EmailBody))
            {
                _logger.LogWarning("SendMail called with empty body for recipient: {Recipient}, Subject: {Subject}",
                    mailData.RecieverMail, mailData.EmailSubject);
            }

            _logger.LogInformation("Attempting to send email to: {Recipient}, Subject: {Subject}",
                mailData.RecieverMail, mailData.EmailSubject);

            try
            {
                // Validate configuration before sending
                var smtpHost = _config["MailSettings:Host"];
                var smtpPort = _config["MailSettings:Port"];
                var senderEmail = _config["MailSettings:Gmail"];
                var senderPassword = _config["MailSettings:Password"];

                if (string.IsNullOrEmpty(smtpHost))
                {
                    _logger.LogError("SMTP Host is not configured in MailSettings");
                    throw new InvalidOperationException("Email service is not properly configured. Please contact support.");
                }

                if (string.IsNullOrEmpty(senderEmail))
                {
                    _logger.LogError("Sender email is not configured in MailSettings");
                    throw new InvalidOperationException("Email service is not properly configured. Please contact support.");
                }

                _logger.LogDebug("Using SMTP host: {Host}, Port: {Port}, Sender: {Sender}",
                    smtpHost, smtpPort, senderEmail);

                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(senderEmail));
                email.To.Add(MailboxAddress.Parse(mailData.RecieverMail));
                email.Subject = mailData.EmailSubject;
                email.Body = new TextPart(TextFormat.Html) { Text = mailData.EmailBody };

                _logger.LogDebug("Email message created successfully. From: {From}, To: {To}, Subject: {Subject}",
                    senderEmail, mailData.RecieverMail, mailData.EmailSubject);

                using var smtp = new MailKit.Net.Smtp.SmtpClient();

                _logger.LogDebug("Connecting to SMTP server...");
                smtp.Connect(
                    smtpHost,
                    int.Parse(smtpPort ?? "587"),
                    SecureSocketOptions.StartTls);

                _logger.LogDebug("Authenticating with SMTP server...");
                smtp.Authenticate(senderEmail, senderPassword);

                _logger.LogDebug("Sending email...");
                smtp.Send(email);

                _logger.LogDebug("Disconnecting from SMTP server...");
                smtp.Disconnect(true);

                _logger.LogInformation("Email sent successfully to: {Recipient}, Subject: {Subject}",
                    mailData.RecieverMail, mailData.EmailSubject);

                return true;
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                _logger.LogError(ex, "Authentication failed while sending email to {Recipient}. Please check email credentials.",
                    mailData.RecieverMail);
                throw new InvalidOperationException("Email service authentication failed. Please check your credentials.", ex);
            }
            catch (MailKit.Net.Smtp.SmtpCommandException ex)
            {
                _logger.LogError(ex, "SMTP command failed while sending email to {Recipient}. StatusCode: {StatusCode}, Message: {Message}",
                    mailData.RecieverMail, ex.StatusCode, ex.Message);

                string errorMessage = ex.StatusCode switch
                {
                    MailKit.Net.Smtp.SmtpStatusCode.MailboxUnavailable => "The recipient email address may be invalid or unavailable.",
                    MailKit.Net.Smtp.SmtpStatusCode.MailboxNameNotAllowed => "The recipient email address format is invalid.",
                    MailKit.Net.Smtp.SmtpStatusCode.ExceededStorageAllocation => "The recipient's mailbox is full.",
                    _ => "Failed to send email due to SMTP server error."
                };

                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (MailKit.Net.Smtp.SmtpProtocolException ex)
            {
                _logger.LogError(ex, "SMTP protocol error while sending email to {Recipient}", mailData.RecieverMail);
                throw new InvalidOperationException("Email service protocol error. Please try again later.", ex);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                _logger.LogError(ex, "Network error while sending email to {Recipient}. Host: {Host}, Port: {Port}",
                    mailData.RecieverMail, _config["MailSettings:Host"], _config["MailSettings:Port"]);
                throw new InvalidOperationException("Unable to connect to email server. Please check your network connection.", ex);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "Timeout occurred while sending email to {Recipient}", mailData.RecieverMail);
                throw new InvalidOperationException("Email sending timed out. Please try again later.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email to {Recipient}, Subject: {Subject}",
                    mailData.RecieverMail, mailData.EmailSubject);
                throw new InvalidOperationException("An error occurred while sending the email. Please try again later.", ex);
            }
        }

        private void ValidateMailConfiguration()
        {
            try
            {
                var smtpHost = _config["MailSettings:Host"];
                var smtpPort = _config["MailSettings:Port"];
                var senderEmail = _config["MailSettings:Gmail"];
                var senderPassword = _config["MailSettings:Password"];

                var missingConfigs = new List<string>();

                if (string.IsNullOrEmpty(smtpHost))
                    missingConfigs.Add("MailSettings:Host");

                if (string.IsNullOrEmpty(smtpPort))
                    missingConfigs.Add("MailSettings:Port");

                if (string.IsNullOrEmpty(senderEmail))
                    missingConfigs.Add("MailSettings:Gmail");

                if (string.IsNullOrEmpty(senderPassword))
                    missingConfigs.Add("MailSettings:Password");

                if (missingConfigs.Any())
                {
                    _logger.LogWarning("MailService configuration is incomplete. Missing settings: {MissingConfigs}",
                        string.Join(", ", missingConfigs));
                }
                else
                {
                    _logger.LogInformation("MailService configuration validated successfully. Host: {Host}, Port: {Port}, Sender: {Sender}",
                        smtpHost, smtpPort, senderEmail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating mail configuration");
            }
        }

        /// <summary>
        /// Async version of SendMail for better performance
        /// </summary>
        public async Task<bool> SendMailAsync(MailData mailData, CancellationToken cancellationToken = default)
        {
            if (mailData == null)
            {
                _logger.LogError("SendMailAsync failed: MailData is null");
                throw new ArgumentNullException(nameof(mailData), "Mail data cannot be null");
            }

            if (string.IsNullOrEmpty(mailData.RecieverMail))
            {
                _logger.LogError("SendMailAsync failed: Recipient email is null or empty");
                throw new ArgumentException("Recipient email cannot be null or empty", nameof(mailData.RecieverMail));
            }

            _logger.LogInformation("Attempting to send email asynchronously to: {Recipient}, Subject: {Subject}",
                mailData.RecieverMail, mailData.EmailSubject);

            try
            {
                var smtpHost = _config["MailSettings:Host"];
                var smtpPort = _config["MailSettings:Port"];
                var senderEmail = _config["MailSettings:Gmail"];
                var senderPassword = _config["MailSettings:Password"];

                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(senderEmail));
                email.To.Add(MailboxAddress.Parse(mailData.RecieverMail));
                email.Subject = mailData.EmailSubject;
                email.Body = new TextPart(TextFormat.Html) { Text = mailData.EmailBody };

                using var smtp = new MailKit.Net.Smtp.SmtpClient();

                await smtp.ConnectAsync(smtpHost, int.Parse(smtpPort ?? "587"), SecureSocketOptions.StartTls, cancellationToken);
                await smtp.AuthenticateAsync(senderEmail, senderPassword, cancellationToken);
                await smtp.SendAsync(email, cancellationToken);
                await smtp.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("Email sent successfully asynchronously to: {Recipient}, Subject: {Subject}",
                    mailData.RecieverMail, mailData.EmailSubject);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email asynchronously to {Recipient}, Subject: {Subject}",
                    mailData.RecieverMail, mailData.EmailSubject);
                throw new InvalidOperationException("An error occurred while sending the email. Please try again later.", ex);
            }
        }
    }
}