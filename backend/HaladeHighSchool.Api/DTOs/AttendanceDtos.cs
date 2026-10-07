using System.ComponentModel.DataAnnotations;
using HaladeHighSchool.Api.Models;

namespace HaladeHighSchool.Api.DTOs;

public record MarkAttendanceRequest
{
    [Required]
    public int StudentId { get; init; }

    [Required]
    public int SubjectId { get; init; }

    [Required]
    public DateOnly AttendanceDate { get; init; }

    [Required]
    [RegularExpression("^(Present|Absent|Late|Excused)$")]
    public string Status { get; init; } = AttendanceStatuses.Present;

    [MaxLength(300)]
    public string? Remark { get; init; }
}

public record BulkMarkAttendanceRequest
{
    [Required]
    public int SectionId { get; init; }

    [Required]
    public int SubjectId { get; init; }

    [Required]
    public DateOnly AttendanceDate { get; init; }

    [Required, MinLength(1)]
    public List<BulkAttendanceEntry> Entries { get; init; } = [];
}

public record BulkAttendanceEntry
{
    [Required]
    public int StudentId { get; init; }

    [Required]
    [RegularExpression("^(Present|Absent|Late|Excused)$")]
    public string Status { get; init; } = AttendanceStatuses.Present;

    [MaxLength(300)]
    public string? Remark { get; init; }
}

public record AttendanceResponse
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public DateOnly AttendanceDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public int? RecordedByTeacherId { get; init; }
    public string? RecordedByTeacherName { get; init; }
    public string? Remark { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record AttendanceSummaryResponse
{
    public int StudentId { get; init; }
    public string StudentIdNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public int PresentCount { get; init; }
    public int AbsentCount { get; init; }
    public int LateCount { get; init; }
    public int ExcusedCount { get; init; }
    public int TotalCount { get; init; }
}
