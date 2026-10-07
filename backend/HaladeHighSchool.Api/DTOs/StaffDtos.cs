using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HaladeHighSchool.Api.DTOs;

public record CreateStaffRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [MinLength(8), MaxLength(100)]
    public string? Password { get; init; }

    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [JsonIgnore]
    public IFormFile? Photo { get; init; }
}

public record CreateStaffResponse
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? CardUID { get; init; }
    public string? TemporaryPassword { get; init; }
}
