using HaladeHighSchool.Api.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HaladeHighSchool.Api.Services;

public sealed class ReportPdfService(
    ApplicationDbContext db,
    ISystemSettingsService settings) : IReportPdfService
{
    private const decimal PassMarkPercentage = 50m;

    static ReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]?> GenerateMyResultsAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var student = await db.Students
            .AsNoTracking()
            .Where(row => row.IsActive && row.UserId == userId)
            .Select(row => new
            {
                row.Id,
                row.StudentIdNumber,
                FullName = row.User != null ? row.User.FullName : row.StudentIdNumber,
                Grade = row.GradeLevel != null ? row.GradeLevel.Name : string.Empty,
                Section = row.Section != null ? row.Section.Name : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return null;
        }

        var assessments = await db.Marks
            .AsNoTracking()
            .Where(mark => mark.StudentId == student.Id && mark.IsPublished)
            .OrderBy(mark => mark.Subject!.SubjectName)
            .ThenBy(mark => mark.Assessment!.Title)
            .Select(mark => new ResultLine(
                mark.Subject!.SubjectName,
                mark.Assessment!.Title,
                mark.Score,
                mark.Assessment.MaxScore))
            .ToListAsync(cancellationToken);

        var totalObtained = assessments.Sum(row => row.ObtainedScore);
        var totalPossible = assessments.Sum(row => row.MaximumScore);
        var percentage = totalPossible > 0
            ? totalObtained / totalPossible * 100m
            : 0m;
        var finalStatus = percentage >= PassMarkPercentage ? "PASSED" : "FAILED";
        var generatedAt = DateTimeOffset.Now;
        var school = await settings.GetSchoolInfoAsync(cancellationToken);

        return BuildPdf(
            school.SchoolName,
            "Individual Academic Result",
            $"Academic year {school.AcademicYear}",
            generatedAt,
            page =>
            {
                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Element(SectionHeading).Column(details =>
                    {
                        details.Spacing(5);
                        details.Item().Text($"Student: {student.FullName}");
                        details.Item().Text($"Student ID: {student.StudentIdNumber}");
                        details.Item().Text($"Grade & Section: {student.Grade} · {student.Section}");
                        details.Item().Text($"Downloaded on: {generatedAt:dd/MM/yyyy 'at' HH:mm}");
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Subject");
                            header.Cell().Element(HeaderCell).Text("Assessment");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Obtained");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Maximum");
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Status");
                        });

                        foreach (var row in assessments)
                        {
                            var passed = row.MaximumScore > 0
                                && row.ObtainedScore / row.MaximumScore * 100m >= PassMarkPercentage;
                            table.Cell().Element(BodyCell).Text(row.SubjectName);
                            table.Cell().Element(BodyCell).Text(row.AssessmentName);
                            table.Cell().Element(BodyCell).AlignRight().Text($"{row.ObtainedScore:0.##}");
                            table.Cell().Element(BodyCell).AlignRight().Text($"{row.MaximumScore:0.##}");
                            table.Cell().Element(BodyCell).AlignCenter()
                                .Text(passed ? "PASS" : "FAIL")
                                .FontColor(passed ? Colors.Green.Darken2 : Colors.Red.Darken2)
                                .SemiBold();
                        }
                    });

                    column.Item().Element(SectionHeading).Column(summary =>
                    {
                        summary.Spacing(5);
                        summary.Item().Text($"Total marks: {totalObtained:0.##} / {totalPossible:0.##}");
                        summary.Item().Text($"Overall percentage: {percentage:0.##}%");
                        summary.Item().Text(finalStatus)
                            .FontSize(16)
                            .SemiBold()
                            .FontColor(finalStatus == "PASSED" ? Colors.Green.Darken2 : Colors.Red.Darken2);
                    });
                });
            });
    }

    public async Task<byte[]> GenerateStudentsRosterAsync(
        int? gradeLevelId,
        int? sectionId,
        CancellationToken cancellationToken)
    {
        var query = db.Students.AsNoTracking();

        if (gradeLevelId is int grade)
        {
            query = query.Where(student => student.GradeLevelId == grade);
        }

        if (sectionId is int section)
        {
            query = query.Where(student => student.SectionId == section);
        }

        var students = await query
            .OrderBy(student => student.GradeLevel!.Level)
            .ThenBy(student => student.Section!.Code)
            .ThenBy(student => student.StudentIdNumber)
            .Select(student => new
            {
                student.StudentIdNumber,
                FullName = student.User != null ? student.User.FullName : student.StudentIdNumber,
                Grade = student.GradeLevel != null ? student.GradeLevel.Name : string.Empty,
                Section = student.Section != null ? student.Section.Name : string.Empty,
                GuardianContact = student.GuardianPhone,
                student.IsActive
            })
            .ToListAsync(cancellationToken);

        var school = await settings.GetSchoolInfoAsync(cancellationToken);
        var title = sectionId is null ? "Enrolled Students Roster" : "Section Students Roster";
        var subtitle = gradeLevelId is int gradeFilter && sectionId is int sectionFilter
            ? $"{await GetGradeNameAsync(gradeFilter, cancellationToken)} · {await GetSectionNameAsync(sectionFilter, cancellationToken)}"
            : sectionId is int selectedSection
                ? await GetSectionNameAsync(selectedSection, cancellationToken)
                : "All grades and sections";
        var generatedAt = DateTimeOffset.Now;

        return BuildPdf(
            school.SchoolName,
            title,
            $"Academic year {school.AcademicYear} · {subtitle} · Total students: {students.Count}",
            generatedAt,
            page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Generated: {generatedAt:dd/MM/yyyy 'at' HH:mm}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1.6f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Student ID");
                            header.Cell().Element(HeaderCell).Text("Full Name");
                            header.Cell().Element(HeaderCell).Text("Grade");
                            header.Cell().Element(HeaderCell).Text("Section");
                            header.Cell().Element(HeaderCell).Text("Guardian Contact");
                            header.Cell().Element(HeaderCell).Text("Approval Status");
                        });

                        foreach (var student in students)
                        {
                            table.Cell().Element(BodyCell).Text(student.StudentIdNumber);
                            table.Cell().Element(BodyCell).Text(student.FullName);
                            table.Cell().Element(BodyCell).Text(student.Grade);
                            table.Cell().Element(BodyCell).Text(student.Section);
                            table.Cell().Element(BodyCell).Text(student.GuardianContact ?? "—");
                            table.Cell().Element(BodyCell)
                                .Text(student.IsActive ? "Approved · Active" : "Approved · Inactive");
                        }
                    });
                });
            });
    }

    public async Task<byte[]> GenerateTeachersRosterAsync(CancellationToken cancellationToken)
    {
        var teachers = await db.Teachers
            .AsNoTracking()
            .OrderBy(teacher => teacher.EmployeeId)
            .Select(teacher => new
            {
                teacher.Id,
                teacher.EmployeeId,
                FullName = teacher.User != null ? teacher.User.FullName : teacher.EmployeeId,
                Email = teacher.User != null ? teacher.User.Email : null
            })
            .ToListAsync(cancellationToken);

        var assignments = await db.TeacherSubjects
            .AsNoTracking()
            .Where(assignment => assignment.IsActive)
            .Select(assignment => new
            {
                assignment.TeacherId,
                Subject = assignment.Subject != null ? assignment.Subject.SubjectName : string.Empty,
                Section = assignment.Section != null
                    ? $"{assignment.Subject!.GradeLevel!.Name} · {assignment.Section.Name}"
                    : string.Empty
            })
            .ToListAsync(cancellationToken);

        var assignmentMap = assignments
            .GroupBy(assignment => assignment.TeacherId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Subjects = string.Join(", ", group.Select(row => row.Subject).Distinct().Order()),
                    Sections = string.Join(", ", group.Select(row => row.Section).Distinct().Order())
                });

        var rows = teachers.Select(teacher =>
        {
            assignmentMap.TryGetValue(teacher.Id, out var assigned);
            return new TeacherRosterRow(
                teacher.EmployeeId,
                teacher.FullName,
                teacher.Email ?? "—",
                string.IsNullOrWhiteSpace(assigned?.Subjects) ? "—" : assigned.Subjects,
                string.IsNullOrWhiteSpace(assigned?.Sections) ? "—" : assigned.Sections);
        }).ToList();

        var school = await settings.GetSchoolInfoAsync(cancellationToken);
        var generatedAt = DateTimeOffset.Now;
        return BuildPdf(
            school.SchoolName,
            "Official Faculty Directory",
            $"Total teachers: {rows.Count}",
            generatedAt,
            page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Generated: {generatedAt:dd/MM/yyyy 'at' HH:mm}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Staff ID");
                            header.Cell().Element(HeaderCell).Text("Teacher Full Name");
                            header.Cell().Element(HeaderCell).Text("School Email");
                            header.Cell().Element(HeaderCell).Text("Assigned Subjects");
                            header.Cell().Element(HeaderCell).Text("Assigned Sections");
                        });

                        foreach (var teacher in rows)
                        {
                            table.Cell().Element(BodyCell).Text(teacher.StaffId);
                            table.Cell().Element(BodyCell).Text(teacher.FullName);
                            table.Cell().Element(BodyCell).Text(teacher.Email);
                            table.Cell().Element(BodyCell).Text(teacher.Subjects);
                            table.Cell().Element(BodyCell).Text(teacher.Sections);
                        }
                    });
                });
            });
    }

    public async Task<byte[]?> GenerateTeacherSectionRosterAsync(
        string? userId,
        int sectionId,
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

        var assignments = await db.TeacherSubjects
            .AsNoTracking()
            .Where(assignment =>
                assignment.TeacherId == teacherId &&
                assignment.SectionId == sectionId &&
                assignment.IsActive &&
                assignment.Subject != null)
            .Select(assignment => new
            {
                assignment.SubjectId,
                GradeLevelId = assignment.Subject!.GradeLevelId,
                Grade = assignment.Subject.GradeLevel != null
                    ? assignment.Subject.GradeLevel.Name
                    : string.Empty,
                Section = assignment.Section != null ? assignment.Section.Name : string.Empty,
                assignment.AcademicYear
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return null;
        }

        var gradeIds = assignments.Select(assignment => assignment.GradeLevelId).Distinct().ToArray();
        var subjectIds = assignments.Select(assignment => assignment.SubjectId).Distinct().ToArray();
        var students = await db.Students
            .AsNoTracking()
            .Where(student =>
                student.IsActive &&
                student.SectionId == sectionId &&
                gradeIds.Contains(student.GradeLevelId))
            .OrderBy(student => student.StudentIdNumber)
            .Select(student => new
            {
                student.Id,
                student.StudentIdNumber,
                FullName = student.User != null ? student.User.FullName : student.StudentIdNumber
            })
            .ToListAsync(cancellationToken);

        var studentIds = students.Select(student => student.Id).ToArray();
        var resultRows = studentIds.Length == 0
            ? []
            : await db.StudentSubjectPerformances
                .AsNoTracking()
                .Where(result =>
                    studentIds.Contains(result.StudentId) &&
                    subjectIds.Contains(result.SubjectId))
                .Select(result => new { result.StudentId, result.TotalScore })
                .ToListAsync(cancellationToken);
        var performance = resultRows
            .GroupBy(result => result.StudentId)
            .ToDictionary(group => group.Key, group => group.Average(result => result.TotalScore));

        var attendanceRows = studentIds.Length == 0
            ? []
            : await db.Attendance
                .AsNoTracking()
                .Where(attendance =>
                    studentIds.Contains(attendance.StudentId) &&
                    attendance.Status != "Excused")
                .GroupBy(attendance => attendance.StudentId)
                .Select(group => new
                {
                    StudentId = group.Key,
                    Total = group.Count(),
                    Attended = group.Count(attendance =>
                        attendance.Status == "Present" || attendance.Status == "Late")
                })
                .ToListAsync(cancellationToken);
        var attendance = attendanceRows.ToDictionary(
            row => row.StudentId,
            row => row.Total == 0 ? (decimal?)null : (decimal)row.Attended / row.Total * 100m);

        var rows = students.Select((student, index) =>
        {
            performance.TryGetValue(student.Id, out var average);
            attendance.TryGetValue(student.Id, out var attendancePercentage);
            return new SectionRosterRow(
                index + 1,
                student.StudentIdNumber,
                student.FullName,
                attendancePercentage,
                performance.ContainsKey(student.Id)
                    ? average >= PassMarkPercentage ? "Pass" : "Fail"
                    : "Not graded");
        }).ToList();

        var school = await settings.GetSchoolInfoAsync(cancellationToken);
        var grades = string.Join(", ", assignments.Select(assignment => assignment.Grade).Distinct());
        var sections = string.Join(", ", assignments.Select(assignment => assignment.Section).Distinct());
        var years = string.Join(", ", assignments.Select(assignment => assignment.AcademicYear).Distinct());
        var generatedAt = DateTimeOffset.Now;

        return BuildPdf(
            school.SchoolName,
            "Teacher Section Roster",
            $"{grades} · {sections} · {years} · Total students: {rows.Count}",
            generatedAt,
            page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Generated: {generatedAt:dd/MM/yyyy 'at' HH:mm}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(55);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Roll No");
                            header.Cell().Element(HeaderCell).Text("Student ID");
                            header.Cell().Element(HeaderCell).Text("Full Name");
                            header.Cell().Element(HeaderCell).Text("Attendance");
                            header.Cell().Element(HeaderCell).Text("Academic Status");
                        });

                        foreach (var row in rows)
                        {
                            table.Cell().Element(BodyCell).AlignCenter().Text(row.RollNumber.ToString());
                            table.Cell().Element(BodyCell).Text(row.StudentId);
                            table.Cell().Element(BodyCell).Text(row.FullName);
                            table.Cell().Element(BodyCell).AlignCenter()
                                .Text(row.AttendancePercentage is decimal percentage
                                    ? $"{percentage:0.##}%"
                                    : "No records");
                            var passed = row.AcademicStatus == "Pass";
                            table.Cell().Element(BodyCell).AlignCenter()
                                .Text(row.AcademicStatus)
                                .FontColor(passed ? Colors.Green.Darken2
                                    : row.AcademicStatus == "Fail" ? Colors.Red.Darken2 : Colors.Grey.Darken2)
                                .SemiBold();
                        }
                    });
                });
            });
    }

    private async Task<string> GetGradeNameAsync(int gradeLevelId, CancellationToken cancellationToken) =>
        await db.GradeLevels.AsNoTracking()
            .Where(grade => grade.Id == gradeLevelId)
            .Select(grade => grade.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Grade {gradeLevelId}";

    private async Task<string> GetSectionNameAsync(int sectionId, CancellationToken cancellationToken) =>
        await db.Sections.AsNoTracking()
            .Where(section => section.Id == sectionId)
            .Select(section => section.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Section {sectionId}";

    private static byte[] BuildPdf(
        string schoolName,
        string title,
        string subtitle,
        DateTimeOffset generatedAt,
        Action<PageDescriptor> composePage)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.Header().Column(header =>
                {
                    header.Item().Text(string.IsNullOrWhiteSpace(schoolName) ? "School" : schoolName)
                        .FontSize(18)
                        .SemiBold()
                        .FontColor(Colors.Blue.Darken3);
                    header.Item().Text(title).FontSize(12).SemiBold();
                    header.Item().Text(subtitle).FontSize(9).FontColor(Colors.Grey.Darken2);
                });
                composePage(page);
                page.Footer().Row(footer =>
                {
                    footer.RelativeItem().Text($"Generated {generatedAt:dd/MM/yyyy 'at' HH:mm}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                    footer.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" of ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });
            });
        }).GeneratePdf();
    }

    private static IContainer SectionHeading(IContainer container) =>
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5)
            .Padding(10);

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Background(Colors.Blue.Darken3)
            .PaddingVertical(6)
            .PaddingHorizontal(5)
            .DefaultTextStyle(style => style.FontColor(Colors.White).SemiBold().FontSize(8));

    private static IContainer BodyCell(IContainer container) =>
        container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .PaddingHorizontal(5)
            .DefaultTextStyle(style => style.FontSize(8));

    private sealed record ResultLine(
        string SubjectName,
        string AssessmentName,
        decimal ObtainedScore,
        decimal MaximumScore);

    private sealed record TeacherRosterRow(
        string StaffId,
        string FullName,
        string Email,
        string Subjects,
        string Sections);

    private sealed record SectionRosterRow(
        int RollNumber,
        string StudentId,
        string FullName,
        decimal? AttendancePercentage,
        string AcademicStatus);
}
