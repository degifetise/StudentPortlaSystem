using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

public record CreateGuardianRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [MinLength(8), MaxLength(100)]
    public string? Password { get; init; }

    [Required, MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }

    [Required]
    public int StudentId { get; init; }

    [MaxLength(50)]
    public string? Relationship { get; init; }
}

public record GuardianLinkResponse
{
    public int GuardianId { get; init; }
    public int StudentId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Relationship { get; init; }
    public bool IsPrimaryContact { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record GuardianStudentSummary
{
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int GradeLevelId { get; init; }
    public string GradeLevelName { get; init; } = string.Empty;
    public int SectionId { get; init; }
    public string SectionName { get; init; } = string.Empty;
    public string Relationship { get; init; } = string.Empty;
}

public record GuardianDetailResponse
{
    public int GuardianId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<GuardianStudentSummary> LinkedStudents { get; init; } = [];
}

/// <summary>Administrator request to create a guardian account without a student link.</summary>
public record RegisterGuardianRequest
{
    [Required, MaxLength(75)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(75)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }
}

/// <summary>New guardian account details. TemporaryPassword is returned only at creation.</summary>
public record RegisteredGuardianResponse
{
    public int GuardianId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? TemporaryPassword { get; init; }
}

/// <summary>Administrator request to connect an existing guardian and student.</summary>
public record LinkGuardianStudentRequest
{
    [Range(1, int.MaxValue)]
    public int GuardianId { get; init; }

    [Range(1, int.MaxValue)]
    public int StudentId { get; init; }

    [Required, MaxLength(50)]
    public string Relationship { get; init; } = string.Empty;
}

/// <summary>Updates the relationship recorded for an existing guardian/student link.</summary>
public record UpdateGuardianLinkRequest
{
    [Range(1, int.MaxValue)]
    public int GuardianId { get; init; }

    [Range(1, int.MaxValue)]
    public int StudentId { get; init; }

    [Required, MaxLength(50)]
    public string Relationship { get; init; } = string.Empty;
}

/// <summary>One linked student and the relationship recorded for an administrator.</summary>
public record AdminGuardianStudentResponse
{
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public string Relationship { get; init; } = string.Empty;
}

/// <summary>Guardian account and linked students returned to administrators.</summary>
public record AdminGuardianResponse
{
    public int GuardianId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public IReadOnlyList<AdminGuardianStudentResponse> LinkedStudents { get; init; } = [];
}
