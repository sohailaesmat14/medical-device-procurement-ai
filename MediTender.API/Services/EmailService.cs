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
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        
        private readonly string _host;
        private readonly int _port;
        private readonly string _email;
        private readonly string _password;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _host = _configuration["EmailSettings:Host"] ?? "smtp.gmail.com";
            _port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            _email = _configuration["EmailSettings:Email"] ?? "";
            _password = _configuration["EmailSettings:Password"] ?? "";
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(_host, _port)
                {
                    Credentials = new NetworkCredential(_email, _password),
                    EnableSsl = true,
                    Timeout = 10000 
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_email, "MediTender"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                
                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "SMTP error occurred while sending email to {To}. Status: {StatusCode}", to, smtpEx.StatusCode);
                throw new Exception("Failed to send email due to a mail server issue. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while sending email to {To}", to);
                throw new Exception("An unexpected error occurred while sending the email.");
            }
        }    
    }
}