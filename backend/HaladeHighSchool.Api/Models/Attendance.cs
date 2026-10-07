namespace HaladeHighSchool.Api.Models;

public static class AttendanceStatuses
{
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string Late = "Late";
    public const string Excused = "Excused";

    public static readonly string[] All = [Present, Absent, Late, Excused];
}

public class Attendance
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public string Status { get; set; } = AttendanceStatuses.Present;

    public int? RecordedByTeacherId { get; set; }

    public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Student? Student { get; set; }

    public Teacher? RecordedByTeacher { get; set; }
}
