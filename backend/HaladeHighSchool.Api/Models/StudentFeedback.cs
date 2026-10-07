namespace HaladeHighSchool.Api.Models;

public static class StudentFeedbackStatuses
{
    public const string PendingReview = "Pending Review";
    public const string Acknowledged = "Acknowledged";
    public const string Resolved = "Resolved";

    public static readonly string[] All = [PendingReview, Acknowledged, Resolved];
}

public class StudentFeedback
{
    public long Id { get; set; }
    public int StudentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = StudentFeedbackStatuses.PendingReview;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Student Student { get; set; } = null!;
}
