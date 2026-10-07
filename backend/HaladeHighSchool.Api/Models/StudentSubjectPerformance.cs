namespace HaladeHighSchool.Api.Models;

/// <summary>
/// Keyless read model over the strict 100-point vw_StudentSubjectPerformance view.
/// </summary>
public class StudentSubjectPerformance
{
    public int StudentId { get; set; }

    public int SubjectId { get; set; }

    public decimal? QuizScore { get; set; }

    public decimal? TestScore { get; set; }

    public decimal? MidExamScore { get; set; }

    public decimal? FinalExamScore { get; set; }

    public decimal TotalScore { get; set; }

    public bool IsPassed { get; set; }

    public string Status { get; set; } = "Fail";
}
