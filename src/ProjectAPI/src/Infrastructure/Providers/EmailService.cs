using System.Net.Mail;
using System.Net;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Infrastructure.Settings;

namespace ProjectAPI.Infrastructure.Providers;
/// <summary>
/// Implementation of the <see cref="IEmailService"/> interface for sending emails.
///
/// Credentials come from <see cref="SmtpSettings"/> rather than being written
/// into this file: the mailbox password previously sat in source control, so
/// rotating it required a code change and anyone with repository access could
/// send mail as the company.
/// </summary>
public class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;

    public EmailService(SmtpSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Sends an email asynchronously.
    /// </summary>
    /// <param name="to">The recipient email address.</param>
    /// <param name="subject">The subject of the email.</param>
    /// <param name="body">The body of the email.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        using (var client = new SmtpClient(_settings.Host, _settings.Port))
        {
            client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
            client.EnableSsl = _settings.EnableSsl;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.ResolvedFrom),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
        }
    }
}
