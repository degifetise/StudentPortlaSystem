namespace HaladeHighSchool.Api.Services;

public interface IPasswordResetService
{
    /// <summary>
    /// Creates a short-lived reset token for an existing account. A null result means the email
    /// was not found; callers must return the same public response either way.
    /// </summary>
    Task<string?> RequestResetAsync(
        string email,
        string? requestedFromIp = null,
        CancellationToken cancellationToken = default);

    /// <summary>Uses a valid reset token once and applies the new password through Identity.</summary>
    Task<PasswordResetResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public record PasswordResetResult
{
    public bool Succeeded { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static PasswordResetResult Ok() => new() { Succeeded = true };

    public static PasswordResetResult Fail(params string[] errors) => new() { Errors = errors };
}
