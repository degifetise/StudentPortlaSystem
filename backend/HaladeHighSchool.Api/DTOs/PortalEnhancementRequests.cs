using System.ComponentModel.DataAnnotations;

namespace HaladeHighSchool.Api.DTOs;

/// <summary>Student feedback submission.</summary>
public sealed record CreateStudentFeedbackRequest
{
    [Required, RegularExpression("^(Subject|Teacher|Marks)$")]
    public required string Category { get; init; }

    [Required, StringLength(4000, MinimumLength = 5)]
    public required string Message { get; init; }
}

/// <summary>Change the review status of submitted student feedback.</summary>
public sealed record UpdateStudentFeedbackStatusRequest
{
    [Required, RegularExpression("^(Pending Review|Acknowledged|Resolved)$")]
    public required string Status { get; init; }
}

/// <summary>Feedback details returned to students and administrators.</summary>
public sealed record StudentFeedbackResponse(
    long Id,
    int StudentId,
    string StudentIdNumber,
    string StudentName,
    string Category,
    string Message,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>New comment posted by a student on a school event.</summary>
public sealed record CreateEventCommentRequest
{
    [Required, StringLength(2000, MinimumLength = 2)]
    public required string CommentText { get; init; }

    public long? ParentCommentId { get; init; }
}

/// <summary>A comment or reply displayed in an event discussion.</summary>
public sealed record EventCommentResponse(
    long Id,
    int EventId,
    string? UserId,
    string UserName,
    string UserRole,
    long? ParentCommentId,
    string CommentText,
    DateTimeOffset CreatedAt);

/// <summary>Teacher assignments to create across the selected subjects and sections.</summary>
public sealed record BulkTeacherAssignmentRequest
{
    [Range(1, int.MaxValue)]
    public required int TeacherId { get; init; }

    [Required, MinLength(1), MaxLength(200)]
    public required IReadOnlyList<int> SubjectIds { get; init; }

    [Required, MinLength(1), MaxLength(100)]
    public required IReadOnlyList<int> SectionIds { get; init; }
}

/// <summary>Student IDs and class destination for a batch enrollment update.</summary>
public sealed record BulkStudentEnrollmentRequest
{
    [Required, MinLength(1), MaxLength(250)]
    public required IReadOnlyList<string> StudentIdNumbers { get; init; }

    [Range(1, int.MaxValue)]
    public required int GradeLevelId { get; init; }

    [Range(1, int.MaxValue)]
    public required int SectionId { get; init; }
}

/// <summary>Summary returned after a batch teacher assignment.</summary>
public sealed record BulkTeacherAssignmentResponse(int Created, int Reactivated, int AlreadyActive);

/// <summary>Summary returned after a batch student enrollment.</summary>
public sealed record BulkStudentEnrollmentResponse(int Updated, IReadOnlyList<string> StudentIdNumbers);
