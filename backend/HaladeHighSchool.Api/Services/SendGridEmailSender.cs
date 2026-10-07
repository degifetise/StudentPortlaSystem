using HaladeHighSchool.Api.Configuration;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace HaladeHighSchool.Api.Services;

/// <summary>Sends operational email through SendGrid using the configured sender identity.</summary>
public class SendGridEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        IOptions<EmailSettings> settings,
        ILogger<SendGridEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string plainTextContent,
        CancellationToken cancellationToken = default)
    {
        var trimmedTo = toEmail.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTo) || string.IsNullOrWhiteSpace(_settings.SendGridApiKey))
        {
            _logger.LogWarning("Email send skipped because the recipient or API key is missing.");
            return;
        }

        var client = new SendGridClient(_settings.SendGridApiKey);
        var from = new EmailAddress(_settings.FromAddress, _settings.FromName);
        var to = new EmailAddress(trimmedTo);
        var message = MailHelper.CreateSingleEmail(
            from,
            to,
            subject,
            plainTextContent,
            htmlContent);

        var response = await client.SendEmailAsync(message, cancellationToken);
        if ((int)response.StatusCode >= 400)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"SendGrid rejected email to '{trimmedTo}' with status {(int)response.StatusCode}: {body}");
        }
    }
}
