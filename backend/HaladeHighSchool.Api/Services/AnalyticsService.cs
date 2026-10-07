using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

public sealed class AnalyticsService(ApplicationDbContext db) : IAnalyticsService
{
    private const decimal PassMarkPercentage = 50m;

    public async Task<AdminAnalyticsOverviewResponse> GetAdminOverviewAsync(
        CancellationToken cancellationToken)
    {
        var totalStudents = await db.Students
            .AsNoTracking()
            .CountAsync(student => student.IsActive, cancellationToken);
        var totalTeachers = await db.Teachers
            .AsNoTracking()
            .CountAsync(teacher => teacher.IsActive, cancellationToken);

        var gradeCounts = await db.Students
            .AsNoTracking()
            .Where(student => student.IsActive && student.GradeLevel != null)
            .GroupBy(student => student.GradeLevel!.Level)
            .Select(group => new { GradeLevel = group.Key, StudentCount = group.Count() })
            .ToDictionaryAsync(row => row.GradeLevel, row => row.StudentCount, cancellationToken);

        var studentAverages = await (
            from performance in db.StudentSubjectPerformances.AsNoTracking()
            join student in db.Students.AsNoTracking()
                on performance.StudentId equals student.Id
            where student.IsActive
            group performance by student.Id into studentResults
            select studentResults.Average(result => result.TotalScore))
            .ToListAsync(cancellationToken);

        var gradeDistribution = Enumerable.Range(9, 4)
            .Select(level => new GradeEnrollmentResponse(
                level,
                gradeCounts.GetValueOrDefault(level)))
            .ToArray();

        return new AdminAnalyticsOverviewResponse(
            totalStudents,
            totalTeachers,
            gradeDistribution,
            studentAverages.Count,
            studentAverages.Count(average => average >= PassMarkPercentage),
            studentAverages.Count(average => average < PassMarkPercentage),
            PassMarkPercentage);
    }

    public async Task<TeacherAnalyticsPerformanceResponse?> GetTeacherPerformanceAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var teacherId = await db.Teachers
            .AsNoTracking()
            .Where(teacher => teacher.IsActive && teacher.UserId == userId)
            .Select(teacher => (int?)teacher.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (teacherId is null)
        {
            return null;
        }

        var assignedClassCount = await db.TeacherSubjects
            .AsNoTracking()
            .CountAsync(
                assignment => assignment.TeacherId == teacherId && assignment.IsActive,
                cancellationToken);

        var studentAverages = await (
            from assignment in db.TeacherSubjects.AsNoTracking()
            join student in db.Students.AsNoTracking()
                on assignment.SectionId equals student.SectionId
            join performance in db.StudentSubjectPerformances.AsNoTracking()
                on new { StudentId = student.Id, assignment.SubjectId }
                equals new { performance.StudentId, performance.SubjectId }
            where assignment.TeacherId == teacherId
                && assignment.IsActive
                && student.IsActive
                && assignment.Subject != null
                && assignment.Subject.GradeLevelId == student.GradeLevelId
            group performance by student.Id into studentResults
            select studentResults.Average(result => result.TotalScore))
            .ToListAsync(cancellationToken);

        return new TeacherAnalyticsPerformanceResponse(
            assignedClassCount,
            studentAverages.Count,
            studentAverages.Count(average => average >= PassMarkPercentage),
            studentAverages.Count(average => average < PassMarkPercentage),
            PassMarkPercentage);
    }
}
