using HaladeHighSchool.Api.DTOs;

namespace HaladeHighSchool.Api.Services;

public interface IEventService
{
    Task<PaginatedResult<EventDto>> GetEventsAsync(int page, int pageSize, string? search, string? category, string? status, CancellationToken cancellationToken = default, string? targetStakeholders = null, int? gradeLevelId = null, int? sectionId = null, bool includeAllTargets = false);
    Task<EventDto?> GetEventAsync(int id, CancellationToken cancellationToken = default);
    Task<EventDto> CreateEventAsync(CreateEventDto request, string? organizerUserId, CancellationToken cancellationToken = default);
    Task<EventDto?> UpdateEventAsync(int id, UpdateEventDto request, CancellationToken cancellationToken = default);
    Task<EventDto?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default);
    Task<bool> DeleteEventAsync(int id, CancellationToken cancellationToken = default);
    Task<EventRegistrationDto> RegisterAsync(int eventId, int studentId, CancellationToken cancellationToken = default);
    Task<bool> CancelRegistrationAsync(int eventId, int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventRegistrationDto>> GetRegistrationsAsync(int eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventDto>> GetUpcomingEventsAsync(int count, CancellationToken cancellationToken = default);
    Task<int> GetTotalRegistrationsCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventDto>> GetPopularEventsAsync(int top, CancellationToken cancellationToken = default);
}

public sealed class EventServiceException : Exception
{
    public EventServiceException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException) => StatusCode = statusCode;
    public int StatusCode { get; }
}