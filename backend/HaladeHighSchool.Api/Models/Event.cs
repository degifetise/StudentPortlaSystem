namespace HaladeHighSchool.Api.Models;

using System.ComponentModel.DataAnnotations.Schema;

public static class EventStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Ongoing = "Ongoing";
    public const string Completed = "Completed";
    public const string Canceled = "Canceled";
    public const string Cancelled = Canceled;

    public static readonly string[] All = [Upcoming, Ongoing, Completed, Canceled];
}

public class Event
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? BannerImageUrl { get; set; }

    [NotMapped]
    public string? BannerUrl { get => BannerImageUrl; set => BannerImageUrl = value; }

    public string? Venue { get; set; }

    public string Category { get; set; } = "General";

    public string? Organizer { get; set; }

    public DateTime StartsAt { get; set; }

    [NotMapped]
    public DateTime StartDate { get => StartsAt; set => StartsAt = value; }

    public DateTime? EndsAt { get; set; }

    [NotMapped]
    public DateTime? EndDate { get => EndsAt; set => EndsAt = value; }

    public DateTime? RegistrationDeadline { get; set; }

    /// <summary>All, Admin, Teacher or Student.</summary>
    public string TargetRole { get; set; } = "All";

    public string? TargetGradeIdsCsv { get; set; }

    public string? TargetSectionIdsCsv { get; set; }

    /// <summary>Null means every grade.</summary>
    public int? GradeLevelId { get; set; }

    /// <summary>Null means every section.</summary>
    public int? SectionId { get; set; }

    /// <summary>Null means unlimited student registrations.</summary>
    public int? MaxStudents { get; set; }

    [NotMapped]
    public int? MaxAttendees { get => MaxStudents; set => MaxStudents = value; }

    public int MaxGuestsPerStudent { get; set; }

    public string? OrganizedByUserId { get; set; }

    public bool IsPublished { get; set; } = true;

    [NotMapped]
    public bool IsActive { get => IsPublished && !IsCancelled; set => IsPublished = value; }

    public bool IsCancelled { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>Optional Admin override. Null means status is derived from the event dates.</summary>
    public string? StatusOverride { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [NotMapped]
    public string Status => IsCancelled || StatusOverride == EventStatuses.Canceled
        ? EventStatuses.Canceled
        : StatusOverride is EventStatuses.Upcoming or EventStatuses.Ongoing or EventStatuses.Completed
            ? StatusOverride
        : DateTime.UtcNow < StartsAt
            ? EventStatuses.Upcoming
            : EndsAt.HasValue && DateTime.UtcNow >= EndsAt.Value
                ? EventStatuses.Completed
                : EventStatuses.Ongoing;

    public GradeLevel? GradeLevel { get; set; }

    public Section? Section { get; set; }

    public ApplicationUser? OrganizedByUser { get; set; }

    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
    public ICollection<EventComment> Comments { get; set; } = new List<EventComment>();
}
