using System.Net.Mail;
using System.Net;
using SPA.Data;

namespace SPA.Services
{
    public class EmailService
    {
        private readonly SecondDbContext _context;
        /*private readonly ILoggerService _logger;*/
        private readonly IConfiguration _configuration;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _senderPassword;
        private readonly string _recipientEmail;

        public EmailService(SecondDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;

            _smtpServer = _configuration["EmailSettings:Host"];
            _smtpPort = int.Parse(_configuration["EmailSettings:Port"]);
            _senderEmail = _configuration["EmailSettings:Email"];
            _senderPassword = _configuration["EmailSettings:Password"];
            _recipientEmail = _configuration["ErrorEmailRecipient:RecipientEmail"];
        }

        public string SendEmail(string to, string subject, string body)
        {
            var smtpClient = new SmtpClient(_smtpServer)
            {
                Port = _smtpPort,
                Credentials = new NetworkCredential(_senderEmail, _senderPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_senderEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(to);
            try
            {
                smtpClient.Send(mailMessage);
                return "Email sent";
            }
            catch (Exception ex)
            { 
                return ex.Message;
            }
        }
        public void SendErrorEmail(string subject, string error, string errormessage, string OccuranceSpace)
        {
            string body = $@"
                <div style=""text-align: center; background-color: #fff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0, 0, 0, 0.1); border: 2px solid black; min-width: 200px; max-width: 300px; width: 100%; margin: 50px auto;"">
                    <h2 style=""color: blue;"">New Error Found in Application KeyGen <hr /></h2>
                     <p>
                        <strong>Error Occurred At: </strong>{OccuranceSpace}<br /> 
                    </p>
                    <p>
                        <strong>Error: </strong>{error} : {errormessage}<br />
                    </p>
                    <p style=""color: #F00;"">
                        Please look into the error ASAP
                    </p>
                </div>";

            var smtpClient = new SmtpClient(_smtpServer)
            {
                Port = _smtpPort,
                Credentials = new NetworkCredential(_senderEmail, _senderPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_senderEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(_recipientEmail);
            try
            {
                smtpClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
               
            }
        }
    }
}
