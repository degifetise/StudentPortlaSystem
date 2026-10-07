using System.ComponentModel.DataAnnotations;
using HaladeHighSchool.Api.Models;

namespace HaladeHighSchool.Api.DTOs;

public record EventDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? BannerUrl { get; init; }
    public string Category { get; init; } = "General";
    public string? Location { get; init; }
    public string? Organizer { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? RegistrationDeadline { get; init; }
    public int? MaxAttendees { get; init; }
    public string TargetStakeholders { get; init; } = "All";
    public IReadOnlyList<int> TargetGradeIds { get; init; } = [];
    public IReadOnlyList<int> TargetSectionIds { get; init; } = [];
    public string Status { get; init; } = EventStatuses.Upcoming;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int CurrentRegistrationsCount { get; init; }
    public bool IsRegistrationOpen { get; init; }
}

public record CreateEventDto
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? BannerUrl { get; init; }
    public string Category { get; init; } = "General";
    public string? Location { get; init; }
    public string? Organizer { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? RegistrationDeadline { get; init; }
    public int? MaxAttendees { get; init; }

    [RegularExpression("^(All|Students|Teachers)$")]
    public string TargetStakeholders { get; init; } = "All";

    public IReadOnlyList<int> TargetGradeIds { get; init; } = [];

    public IReadOnlyList<int> TargetSectionIds { get; init; } = [];
}

public record UpdateEventDto : CreateEventDto
{
    public bool? IsActive { get; init; }
}

public record UpdateEventStatusDto
{
    public string Status { get; init; } = EventStatuses.Upcoming;
}

public record RegisterEventDto
{
    public int StudentId { get; init; }
}

public record EventRegistrationDto
{
    public int Id { get; init; }
    public int EventId { get; init; }
    public int StudentId { get; init; }
    public DateTime RegistrationDate { get; init; }
    public string Status { get; init; } = EventRegistrationStatuses.Confirmed;
    public string AttendanceStatus { get; init; } = EventAttendanceStatuses.Pending;
}

public record EventDashboardStatsDto
{
    public int TotalRegistrations { get; init; }
    public IReadOnlyList<EventDto> UpcomingEvents { get; init; } = [];
    public IReadOnlyList<EventDto> PopularEvents { get; init; } = [];
}

public record PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}