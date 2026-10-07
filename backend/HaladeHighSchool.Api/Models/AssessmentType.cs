namespace HaladeHighSchool.Api.Models;

/// <summary>
/// Assessment categories persisted as text. Assignment remains for legacy rows but is
/// inactive; new grading uses strict Quiz/Test/MidExam/FinalExam components.
/// </summary>
public enum AssessmentType
{
    Quiz,
    Assignment,
    Test,
    MidExam,
    FinalExam,
    Other
}

/// <summary>
/// Read model over the AssessmentTypes lookup table. The weights live in the
/// database so the report card view and the API always agree.
/// </summary>
public class AssessmentTypeWeight
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal WeightPercentage { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
