using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public record ResetPasswordRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}
