using System.Net;
using System.Net.Mail;

namespace MediTender.API.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var senderEmail = _configuration["EmailSettings:Email"];
            var senderPassword = _configuration["EmailSettings:Password"];

            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            using var mailMessage = new MailMessage(from: senderEmail!, to: toEmail, subject, body)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
        }
    }
}