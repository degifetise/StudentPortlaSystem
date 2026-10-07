namespace HaladeHighSchool.Api.Models;

using System.ComponentModel.DataAnnotations.Schema;

public static class EventRegistrationStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public const string Waitlisted = "Waitlisted";

    public static readonly string[] All = [Confirmed, Cancelled, Waitlisted];
}

public static class EventAttendanceStatuses
{
    public const string Pending = "Pending";
    public const string Attended = "Attended";
    public const string Absent = "Absent";
    public static readonly string[] All = [Pending, Attended, Absent];
}

public class EventRegistration
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public int StudentId { get; set; }

    public int GuestCount { get; set; }

    public string Status { get; set; } = EventRegistrationStatuses.Confirmed;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public DateTime RegistrationDate { get => RegisteredAt; set => RegisteredAt = value; }

    public string AttendanceStatus { get; set; } = EventAttendanceStatuses.Pending;

    public DateTime? CancelledAt { get; set; }

    public Event? Event { get; set; }

    public Student? Student { get; set; }
}
