using SendGrid;
using SendGrid.Helpers.Mail;

namespace DotnetLearning.Services
{
    public class SendGridEmailService : IEmailService
    {
        private readonly string _apiKey;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public SendGridEmailService(IConfiguration configuration)
        {
            _apiKey = configuration["SendGrid:ApiKey"]!;
            _fromEmail = configuration["SendGrid:FromEmail"]!;
            _fromName = configuration["SendGrid:FromName"]!;
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var client = new SendGridClient(_apiKey);
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = subject,
                HtmlContent = htmlBody
            };
            msg.AddTo(new EmailAddress(to));
            await client.SendEmailAsync(msg);
        }
    }
}
