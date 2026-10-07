using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HaladeHighSchool.Api.DTOs;

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record StudentListItem
{
    public int Id { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public int SectionId { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public string? Gender { get; init; }
    public bool IsActive { get; init; }
    public bool HasLogin { get; init; }
    public DateOnly EnrollmentDate { get; init; }
}

public record StudentDetailResponse : StudentListItem
{
    public string? UserId { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? Address { get; init; }
    public string? ProfileImageUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public int MarksCount { get; init; }
}

public record CreateStudentRequest
{
    /// <summary>Optional. A temporary password is generated when omitted.</summary>
    [MinLength(8), MaxLength(100)]
    public string? Password { get; init; }

    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    public int GradeLevelId { get; init; }

    [Required]
    public int SectionId { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female or Other.")]
    public string? Gender { get; init; }

    [MaxLength(150)]
    public string? GuardianName { get; init; }

    [MaxLength(30)]
    public string? GuardianPhone { get; init; }

    [MaxLength(250)]
    public string? Address { get; init; }

    [JsonIgnore]
    public IFormFile? Photo { get; init; }
}

public record CreateStudentResponse
{
    public StudentDetailResponse Student { get; init; } = new();

    /// <summary>Returned once, only when the API generated the password.</summary>
    public string? TemporaryPassword { get; init; }
}

public record UpdateStudentRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female or Other.")]
    public string? Gender { get; init; }

    [MaxLength(150)]
    public string? GuardianName { get; init; }

    [MaxLength(30)]
    public string? GuardianPhone { get; init; }

    [MaxLength(250)]
    public string? Address { get; init; }

    [MaxLength(500)]
    public string? ProfileImageUrl { get; init; }
}

/// <summary>Move a student to a different grade and/or section.</summary>
public record AssignClassRequest
{
    [Required]
    public int GradeLevelId { get; init; }

    [Required]
    public int SectionId { get; init; }
}

public record SetActiveRequest
{
    public bool IsActive { get; init; }
}

public record AcademicYearRolloverRequest : IValidatableObject
{
    [Required]
    [RegularExpression("^[0-9]{4}-[0-9]{4}$", ErrorMessage = "AcademicYear must look like '2026-2027'.")]
    public string NewAcademicYear { get; init; } = string.Empty;

    public bool Confirm { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(NewAcademicYear))
        {
            yield break;
        }

        if (NewAcademicYear.Length != 9 ||
            !int.TryParse(NewAcademicYear[..4], out var start) ||
            !int.TryParse(NewAcademicYear[5..], out var end))
        {
            yield break;
        }

        if (end != start + 1)
        {
            yield return new ValidationResult(
                $"AcademicYear must span two consecutive years, so '{start}-{start + 1}' rather than '{NewAcademicYear}'.",
                [nameof(NewAcademicYear)]);
        }
    }
}

public record AcademicYearRolloverResponse
{
    public string FromAcademicYear { get; init; } = string.Empty;
    public string ToAcademicYear { get; init; } = string.Empty;
    public bool Confirmed { get; init; }
    public int StudentsSeen { get; init; }
    public int PromotedCount { get; init; }
    public int SkippedCount { get; init; }
    public int UpdatedAcademicYear { get; init; }
}

public record ResetPasswordResponse
{
    public string TemporaryPassword { get; init; } = string.Empty;
}
