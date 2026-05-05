using Microsoft.Extensions.Configuration;
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

        public MailService(IConfiguration config)
        {
            _config = config;
        }
        public bool SendMail(MailData mailData)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_config["MailSettings:Gmail"]));
                email.To.Add(MailboxAddress.Parse(mailData.RecieverMail));
                email.Subject = mailData.EmailSubject;
                email.Body = new TextPart(TextFormat.Html) { Text = mailData.EmailBody };

                using var smtp = new MailKit.Net.Smtp.SmtpClient();
                smtp.Connect(
                    _config["MailSettings:Host"],
                    int.Parse(_config["MailSettings:Port"]),
                    SecureSocketOptions.StartTls);
                smtp.Authenticate(
                    _config["MailSettings:Gmail"],
                    _config["MailSettings:Password"]);
                smtp.Send(email);
                smtp.Disconnect(true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
