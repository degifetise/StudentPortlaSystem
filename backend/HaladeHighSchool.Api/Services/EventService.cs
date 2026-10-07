using AutoMapper;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

public class EventService : IEventService
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<EventService> _logger;

    public EventService(IApplicationDbContext db, IMapper mapper, ILogger<EventService> logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PaginatedResult<EventDto>> GetEventsAsync(
        int page,
        int pageSize,
        string? search,
        string? category,
        string? status,
        CancellationToken cancellationToken = default,
        string? targetStakeholders = null,
        int? gradeLevelId = null,
        int? sectionId = null,
        bool includeAllTargets = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = DateTime.UtcNow;
        var query = _db.Events.AsNoTracking().Include(e => e.Registrations)
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (!includeAllTargets)
        {
            var targetRole = targetStakeholders switch
            {
                "Students" => "Student",
                "Teachers" => "Teacher",
                _ => "All"
            };
            query = query.Where(e => e.TargetRole == "All" || e.TargetRole == targetRole);

            if (gradeLevelId is int grade)
            {
                var gradeToken = $"|{grade}|";
                query = query.Where(e => e.TargetGradeIdsCsv == null || e.TargetGradeIdsCsv == ""
                    ? e.GradeLevelId == null || e.GradeLevelId == grade
                    : e.TargetGradeIdsCsv.Contains(gradeToken));
            }
            else
            {
                query = query.Where(e => (e.TargetGradeIdsCsv == null || e.TargetGradeIdsCsv == "") && e.GradeLevelId == null);
            }

            if (sectionId is int section)
            {
                var sectionToken = $"|{section}|";
                query = query.Where(e => e.TargetSectionIdsCsv == null || e.TargetSectionIdsCsv == ""
                    ? e.SectionId == null || e.SectionId == section
                    : e.TargetSectionIdsCsv.Contains(sectionToken));
            }
            else
            {
                query = query.Where(e => (e.TargetSectionIdsCsv == null || e.TargetSectionIdsCsv == "") && e.SectionId == null);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Title.Contains(term) || (e.Description != null && e.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status switch
            {
                EventStatuses.Cancelled => query.Where(e => e.IsCancelled),
                EventStatuses.Upcoming => query.Where(e => !e.IsCancelled && e.StartsAt > now),
                EventStatuses.Ongoing => query.Where(e => !e.IsCancelled && e.StartsAt <= now && (!e.EndsAt.HasValue || e.EndsAt > now)),
                EventStatuses.Completed => query.Where(e => !e.IsCancelled && e.EndsAt.HasValue && e.EndsAt <= now),
                _ => throw new EventServiceException("Unknown event status.", StatusCodes.Status400BadRequest)
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var events = await query.OrderBy(e => e.StartsAt).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PaginatedResult<EventDto>
        {
            Items = events.Select(_mapper.Map<EventDto>).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<EventDto?> GetEventAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Events.AsNoTracking().Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        return entity is null ? null : _mapper.Map<EventDto>(entity);
    }

    public async Task<EventDto> CreateEventAsync(CreateEventDto request, string? organizerUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(organizerUserId))
        {
            throw new EventServiceException("An organizer identity is required.", StatusCodes.Status401Unauthorized);
        }

        var organizerExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == organizerUserId, cancellationToken);
        if (!organizerExists)
        {
            throw new EventServiceException("The organizer account does not exist.", StatusCodes.Status401Unauthorized);
        }

        var entity = _mapper.Map<Event>(request);
        entity.OrganizedByUserId = organizerUserId;
        entity.IsPublished = true;
        _db.Events.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var gradeIds = (request.TargetGradeIds ?? []).Where(id => id > 0).Distinct().ToArray();
            var sectionIds = (request.TargetSectionIds ?? []).Where(id => id > 0).Distinct().ToArray();
            var recipientUserIds = new HashSet<string>(StringComparer.Ordinal);

            if (request.TargetStakeholders is "All" or "Students")
            {
                var studentUserIds = await _db.Students
                    .AsNoTracking()
                    .Where(student => student.IsActive && student.UserId != null)
                    .Where(student => gradeIds.Length == 0 || gradeIds.Contains(student.GradeLevelId))
                    .Where(student => sectionIds.Length == 0 || sectionIds.Contains(student.SectionId))
                    .Select(student => student.UserId!)
                    .ToListAsync(cancellationToken);
                recipientUserIds.UnionWith(studentUserIds);
            }

            if (request.TargetStakeholders is "All" or "Teachers")
            {
                var teacherUserIds = await _db.TeacherSubjects
                    .AsNoTracking()
                    .Where(assignment => assignment.IsActive && assignment.Teacher != null &&
                        assignment.Teacher.IsActive && assignment.Teacher.UserId != null)
                    .Where(assignment => gradeIds.Length == 0 ||
                        (assignment.Subject != null && gradeIds.Contains(assignment.Subject.GradeLevelId)))
                    .Where(assignment => sectionIds.Length == 0 || sectionIds.Contains(assignment.SectionId))
                    .Select(assignment => assignment.Teacher!.UserId!)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                recipientUserIds.UnionWith(teacherUserIds);
            }

            var eventDate = entity.StartsAt.ToString("f", System.Globalization.CultureInfo.InvariantCulture);
            _db.Notifications.AddRange(recipientUserIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = $"New Campus Event: {entity.Title}",
                Message = $"{entity.Title} scheduled for {eventDate} at {entity.Venue ?? "TBA"}",
                Type = "Event",
                TargetUrl = "/events",
                CreatedAt = DateTime.UtcNow
            }));
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Event {EventId} was created but target notification dispatch failed.", entity.Id);
        }

        return _mapper.Map<EventDto>(entity);
    }

    public async Task<EventDto?> UpdateEventAsync(int id, UpdateEventDto request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Events.Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        if (entity is null) return null;
        _mapper.Map(request, entity);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return _mapper.Map<EventDto>(entity);
    }

    public async Task<EventDto?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = status?.Trim();
        if (!EventStatuses.All.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            throw new EventServiceException("Status must be Upcoming, Ongoing, Completed or Canceled.", StatusCodes.Status400BadRequest);

        var entity = await _db.Events.Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        if (entity is null) return null;

        normalizedStatus = EventStatuses.All.First(value => string.Equals(value, normalizedStatus, StringComparison.OrdinalIgnoreCase));
        entity.StatusOverride = normalizedStatus;
        entity.IsCancelled = normalizedStatus == EventStatuses.Canceled;
        entity.IsPublished = normalizedStatus != EventStatuses.Canceled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return _mapper.Map<EventDto>(entity);
    }

    public async Task<bool> DeleteEventAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        if (entity is null) return false;
        entity.IsCancelled = true;
        entity.IsPublished = false;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EventRegistrationDto> RegisterAsync(int eventId, int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RegisterCoreAsync(eventId, studentId, cancellationToken);
        }
        catch (EventServiceException)
        {
            throw;
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Could not persist registration for student {StudentId} and event {EventId}", studentId, eventId);
            throw new EventServiceException(
                "The registration could not be saved because the event or registration changed. Refresh and try again.",
                StatusCodes.Status409Conflict,
                exception);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected registration error for student {StudentId} and event {EventId}", studentId, eventId);
            throw new EventServiceException(
                "The registration could not be completed. Please try again.",
                StatusCodes.Status500InternalServerError,
                exception);
        }
    }

    private async Task<EventRegistrationDto> RegisterCoreAsync(int eventId, int studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var eventEntity = await _db.Events.Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken)
            ?? throw new EventServiceException("Event was not found.", StatusCodes.Status404NotFound);

        if (!await _db.Students.AnyAsync(s => s.Id == studentId && s.IsActive, cancellationToken))
            throw new EventServiceException("Active student was not found.", StatusCodes.Status404NotFound);

        var now = DateTime.UtcNow;
        if (!eventEntity.IsPublished || eventEntity.IsCancelled || eventEntity.StartsAt <= now)
            throw new EventServiceException("Registration is closed for this event.", StatusCodes.Status409Conflict);
        if (eventEntity.RegistrationDeadline.HasValue && eventEntity.RegistrationDeadline.Value < now)
            throw new EventServiceException("The registration deadline has passed.", StatusCodes.Status409Conflict);

        var existing = eventEntity.Registrations.FirstOrDefault(r => r.StudentId == studentId && r.Status == EventRegistrationStatuses.Confirmed);
        if (existing is not null)
            throw new EventServiceException("You are already registered for this event.", StatusCodes.Status409Conflict);

        var currentCount = eventEntity.Registrations.Count(r => r.Status == EventRegistrationStatuses.Confirmed);
        if (eventEntity.MaxStudents.HasValue && currentCount >= eventEntity.MaxStudents.Value)
            throw new EventServiceException("Event is full.", StatusCodes.Status400BadRequest);

        // The database keeps one row per event/student. Reuse a cancelled or waitlisted
        // row instead of inserting a duplicate and violating the unique constraint.
        var registration = eventEntity.Registrations.FirstOrDefault(r => r.StudentId == studentId);
        if (registration is null)
        {
            registration = new EventRegistration { EventId = eventId, StudentId = studentId };
            _db.EventRegistrations.Add(registration);
        }
        else
        {
            registration.Status = EventRegistrationStatuses.Confirmed;
            registration.CancelledAt = null;
            registration.RegisteredAt = DateTime.UtcNow;
            registration.AttendanceStatus = EventAttendanceStatuses.Pending;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return _mapper.Map<EventRegistrationDto>(registration);
    }

    public async Task<bool> CancelRegistrationAsync(int eventId, int studentId, CancellationToken cancellationToken = default)
    {
        var registration = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.EventId == eventId && r.StudentId == studentId && r.Status == EventRegistrationStatuses.Confirmed, cancellationToken);
        if (registration is null) return false;
        registration.Status = EventRegistrationStatuses.Cancelled;
        registration.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<EventRegistrationDto>> GetRegistrationsAsync(int eventId, CancellationToken cancellationToken = default) =>
        (await _db.EventRegistrations.AsNoTracking().Where(r => r.EventId == eventId).OrderByDescending(r => r.RegisteredAt).ToListAsync(cancellationToken))
        .Select(_mapper.Map<EventRegistrationDto>).ToList();

    public async Task<IReadOnlyList<EventDto>> GetUpcomingEventsAsync(int count, CancellationToken cancellationToken = default) =>
        (await _db.Events.AsNoTracking().Include(e => e.Registrations).Where(e => !e.IsDeleted && e.IsPublished && !e.IsCancelled && e.StartsAt > DateTime.UtcNow).OrderBy(e => e.StartsAt).Take(Math.Clamp(count, 1, 20)).ToListAsync(cancellationToken))
        .Select(_mapper.Map<EventDto>).ToList();

    public Task<int> GetTotalRegistrationsCountAsync(CancellationToken cancellationToken = default) =>
        _db.EventRegistrations.CountAsync(r => r.Status == EventRegistrationStatuses.Confirmed, cancellationToken);

    public async Task<IReadOnlyList<EventDto>> GetPopularEventsAsync(int top, CancellationToken cancellationToken = default) =>
        (await _db.Events.AsNoTracking().Include(e => e.Registrations).Where(e => !e.IsDeleted && !e.IsCancelled).OrderByDescending(e => e.Registrations.Count(r => r.Status == EventRegistrationStatuses.Confirmed)).Take(Math.Clamp(top, 1, 20)).ToListAsync(cancellationToken))
        .Select(_mapper.Map<EventDto>).ToList();
}