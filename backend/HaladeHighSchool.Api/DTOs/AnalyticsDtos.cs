namespace HaladeHighSchool.Api.DTOs;

/// <summary>Student enrolment count for one grade level.</summary>
public sealed record GradeEnrollmentResponse(int GradeLevel, int StudentCount);

/// <summary>School-wide enrolment and published-result pass/fail totals.</summary>
public sealed record AdminAnalyticsOverviewResponse(
    int TotalStudents,
    int TotalTeachers,
    IReadOnlyList<GradeEnrollmentResponse> GradeDistribution,
    int StudentsWithResults,
    int PassedStudents,
    int FailedStudents,
    decimal PassMarkPercentage);

/// <summary>Published-result pass/fail totals for a teacher's assigned classes.</summary>
public sealed record TeacherAnalyticsPerformanceResponse(
    int AssignedClassCount,
    int StudentsWithResults,
    int PassedStudents,
    int FailedStudents,
    decimal PassMarkPercentage);
