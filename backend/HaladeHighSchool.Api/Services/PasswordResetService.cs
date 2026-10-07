using System.Security.Cryptography;
using System.Text;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaladeHighSchool.Api.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IOptions<EmailSettings> emailSettings,
        ILogger<PasswordResetService> logger)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task<string?> RequestResetAsync(
        string email,
        string? requestedFromIp = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var request = new PasswordResetRequest
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            RequestedFromIp = Truncate(requestedFromIp, 45),
            CreatedAt = DateTime.UtcNow,
        };

        _db.PasswordResetRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        var resetUrl = BuildResetUrl(token);
        var displayName = string.IsNullOrWhiteSpace(user.FullName) ? "Student" : user.FullName;
        var plainText =
            $"Hello {displayName},\n\n" +
            "You requested a password reset for your School Management System account. " +
            $"Use the link below within 30 minutes:\n\n{resetUrl}\n\n" +
            "If you did not request this, you can ignore this email.";

        var html = new StringBuilder();
        html.Append("<p>Hello ");
        html.Append(System.Net.WebUtility.HtmlEncode(displayName));
        html.Append(",</p>");
        html.Append("<p>You requested a password reset for your School Management System account.</p>");
        html.Append("<p>Use the link below within 30 minutes:</p>");
        html.Append("<p><a href=\"");
        html.Append(System.Net.WebUtility.HtmlEncode(resetUrl));
        html.Append("\">Reset my password</a></p>");
        html.Append("<p>If you did not request this, you can ignore this email.</p>");

        try
        {
            await _emailSender.SendEmailAsync(
                user.Email ?? email,
                "Reset your School Management System password",
                html.ToString(),
                plainText,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Password reset email failed for user {UserId} [{Email}]",
                user.Id,
                user.Email);
        }

        _logger.LogInformation("Password reset requested for user {UserId}", user.Id);
        return token;
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.PasswordResetRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(
                r => r.Token == token && r.UsedAt == null && r.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (request?.User is null || !request.User.IsActive)
        {
            return PasswordResetResult.Fail("The reset token is invalid or has expired.");
        }

        var identityToken = await _userManager.GeneratePasswordResetTokenAsync(request.User);
        var addResult = await _userManager.ResetPasswordAsync(request.User, identityToken, newPassword);
        if (!addResult.Succeeded)
        {
            return PasswordResetResult.Fail([.. addResult.Errors.Select(e => e.Description)]);
        }

        request.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _userManager.UpdateSecurityStampAsync(request.User);

        _logger.LogInformation("Password reset completed for user {UserId}", request.UserId);
        return PasswordResetResult.Ok();
    }

    private string BuildResetUrl(string token)
    {
        var baseUrl = _emailSettings.FrontendBaseUrl;
        var host = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://localhost:5173"
            : baseUrl.TrimEnd('/');

        return $"{host}/reset-password?token={Uri.EscapeDataString(token)}";
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
