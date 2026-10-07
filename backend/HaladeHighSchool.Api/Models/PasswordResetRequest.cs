namespace HaladeHighSchool.Api.Models;

/// <summary>A one-time password reset token issued to an existing account.</summary>
public class PasswordResetRequest
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public string? RequestedFromIp { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}
