namespace HaladeHighSchool.Api.Configuration;

/// <summary>
/// Email delivery configuration. Values are supplied through appsettings or the user-secrets
/// store, keeping the API key out of source control.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public string SendGridApiKey { get; set; } = string.Empty;

    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
