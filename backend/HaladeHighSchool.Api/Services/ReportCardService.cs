using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

public interface IReportCardService
{
    /// <summary>
    /// One student's weighted results per subject. Null when no such student exists.
    /// Used by the staff-facing report card endpoint.
    /// </summary>
    Task<ReportCardResponse?> BuildAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same figures plus the headline summary and the weighting they were derived from, so
    /// a student's results screen is one request rather than three.
    /// </summary>
    Task<MyResultsResponse?> BuildMyResultsAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Published Grade 11-12 results grouped into a cumulative transcript.</summary>
    Task<TranscriptResponse?> BuildTranscriptAsync(int studentId, CancellationToken cancellationToken = default);
}

/// <summary>
    /// Composes report cards from the strict numerical vw_StudentSubjectPerformance view.
/// </summary>
public class ReportCardService : IReportCardService
{
    private readonly ApplicationDbContext _db;
    private readonly ISystemSettingsService _settings;
    private readonly IGradingPolicyService _grading;

    public ReportCardService(
        ApplicationDbContext db,
        ISystemSettingsService settings,
        IGradingPolicyService grading)
    {
        _db = db;
        _settings = settings;
        _grading = grading;
    }

    public async Task<ReportCardResponse?> BuildAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new
            {
                s.Id,
                s.StudentIdNumber,
                StudentName = s.User != null ? s.User.FullName : s.StudentIdNumber,
                GradeLevelName = s.GradeLevel != null ? s.GradeLevel.Name : string.Empty,
                SectionName = s.Section != null ? s.Section.Name : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return null;
        }

        var rows = await (
            from performance in _db.StudentSubjectPerformances
            join subject in _db.Subjects on performance.SubjectId equals subject.Id
            where performance.StudentId == studentId
            orderby subject.Code
            select new
            {
                performance.SubjectId,
                subject.SubjectName,
                subject.Code,
                performance.QuizScore,
                performance.TestScore,
                performance.MidExamScore,
                performance.FinalExamScore,
                performance.TotalScore,
                performance.IsPassed,
                performance.Status,
            }).ToListAsync(cancellationToken);

        const decimal passMark = 50m;
        var academicYear = await _settings.GetAcademicYearAsync(cancellationToken);

        var subjects = rows.Select(r => new SubjectReportCard
        {
            SubjectId = r.SubjectId,
            SubjectName = r.SubjectName,
            SubjectCode = r.Code,
            QuizScore = r.QuizScore,
            AssignmentScore = null,
            TestScore = r.TestScore,
            MidExamScore = r.MidExamScore,
            FinalExamScore = r.FinalExamScore,
            TotalScore = r.TotalScore,
            IsPassed = r.IsPassed,
            Status = r.Status,
        }).ToList();

        return new ReportCardResponse
        {
            StudentId = student.Id,
            StudentIdNumber = student.StudentIdNumber,
            StudentName = student.StudentName,
            GradeLevelName = student.GradeLevelName,
            SectionName = student.SectionName,
            AcademicYear = academicYear,
            PassMarkPercentage = passMark,
            AverageTotal = subjects.Count == 0
                ? null
                : Math.Round(subjects.Average(s => s.TotalScore), 2),
            Subjects = subjects,
        };
    }

    public async Task<MyResultsResponse?> BuildMyResultsAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var card = await BuildAsync(studentId, cancellationToken);

        if (card is null)
        {
            return null;
        }

        var weights = await _grading.GetActiveAsync(cancellationToken);

        /* A subject only appears in the view once it has a published mark, so "marked" is the
           count of components that carry one rather than the number of subjects. */
        var strongest = card.Subjects.OrderByDescending(s => s.TotalScore).FirstOrDefault();
        var weakest = card.Subjects.OrderBy(s => s.TotalScore).FirstOrDefault();

        var summary = new ResultsSummary
        {
            SubjectCount = card.Subjects.Count,
            SubjectsPassed = card.Subjects.Count(s => s.IsPass),
            WeightedAverage = card.AverageTotal,
            ComponentsMarked = card.Subjects.Sum(s => new[]
            {
                s.QuizScore, s.TestScore, s.MidExamScore, s.FinalExamScore,
            }.Count(score => score is not null)),
            StrongestSubject = strongest?.SubjectName,
            StrongestSubjectTotal = strongest?.TotalScore,
            WeakestSubject = card.Subjects.Count > 1 ? weakest?.SubjectName : null,
            WeakestSubjectTotal = card.Subjects.Count > 1 ? weakest?.TotalScore : null,
        };

        return new MyResultsResponse
        {
            StudentId = card.StudentId,
            StudentIdNumber = card.StudentIdNumber,
            StudentName = card.StudentName,
            GradeLevelName = card.GradeLevelName,
            SectionName = card.SectionName,
            AcademicYear = card.AcademicYear,
            PassMarkPercentage = card.PassMarkPercentage,
            Subjects = card.Subjects,
            Summary = summary,
            GradingWeights = weights,
        };
    }

    public async Task<TranscriptResponse?> BuildTranscriptAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.Id == studentId)
            .Select(s => new
            {
                s.Id,
                s.StudentIdNumber,
                StudentName = s.User != null ? s.User.FullName : s.StudentIdNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return null;
        }

        var marks = await _db.Marks
            .AsNoTracking()
            .Where(m => m.StudentId == studentId && m.IsPublished)
            .Where(m => m.Assessment != null && m.Subject != null && m.Subject.GradeLevel != null
                && m.Subject.GradeLevel.Level >= 11 && m.Subject.GradeLevel.Level <= 12)
            .Select(m => new
            {
                m.Score,
                MaxScore = m.Assessment!.MaxScore,
                AssessmentType = m.Assessment.AssessmentType,
                AcademicYear = m.Assessment.AcademicYear,
                SubjectId = m.SubjectId,
                SubjectName = m.Subject!.SubjectName,
                SubjectCode = m.Subject.Code,
                CreditHours = m.Subject.CreditHours,
                GradeLevel = m.Subject.GradeLevel!.Level,
                GradeLevelName = m.Subject.GradeLevel.Name
            })
            .ToListAsync(cancellationToken);

        var weights = await _grading.GetWeightMapAsync(cancellationToken);
        var subjects = marks
            .GroupBy(m => new
            {
                m.AcademicYear,
                m.SubjectId,
                m.SubjectName,
                m.SubjectCode,
                m.CreditHours,
                m.GradeLevel,
                m.GradeLevelName
            })
            .Select(group =>
            {
                var total = group
                    .GroupBy(m => m.AssessmentType)
                    .Sum(type =>
                    {
                        var possible = type.Sum(m => m.MaxScore);
                        var weight = weights.GetValueOrDefault(type.Key.ToString());
                        return possible == 0 ? 0 : type.Sum(m => m.Score) / possible * weight;
                    });
                return new TranscriptSubjectResult
                {
                    AcademicYear = group.Key.AcademicYear,
                    GradeLevel = group.Key.GradeLevel,
                    GradeLevelName = group.Key.GradeLevelName,
                    SubjectId = group.Key.SubjectId,
                    SubjectName = group.Key.SubjectName,
                    SubjectCode = group.Key.SubjectCode,
                    CreditHours = group.Key.CreditHours,
                    TotalScore = Math.Round(total, 2),
                    IsPassed = total >= 50m,
                    Status = total >= 50m ? "Pass" : "Fail"
                };
            })
            .OrderBy(result => result.AcademicYear)
            .ThenBy(result => result.SubjectCode)
            .ToList();

        var terms = subjects
            .GroupBy(subject => new { subject.AcademicYear, subject.GradeLevel })
            .OrderBy(term => term.Key.AcademicYear)
            .ThenBy(term => term.Key.GradeLevel)
            .Select(term => new TranscriptTermSummary
            {
                AcademicYear = term.Key.AcademicYear,
                GradeLevel = term.Key.GradeLevel,
                CreditHours = term.Sum(subject => subject.CreditHours),
                Subjects = term.ToList()
            })
            .ToList();

        return new TranscriptResponse
        {
            StudentId = student.Id,
            StudentIdNumber = student.StudentIdNumber,
            StudentName = student.StudentName,
            TotalCreditHours = subjects.Sum(subject => subject.CreditHours),
            Terms = terms
        };
    }
}
