namespace HaladeHighSchool.Api.Services;

/// <summary>Abstracts outbound email so the API can email users without coupling to a provider.</summary>
public interface IEmailSender
{
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string plainTextContent,
        CancellationToken cancellationToken = default);
}
