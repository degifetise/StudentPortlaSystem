using System.Security.Claims;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : PortalControllerBase
{
    private readonly IEventService _events;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventService events,
        IApplicationDbContext db,
        ILogger<EventsController> logger)
    {
        _events = events;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [EndpointSummary("List events with pagination and filters")]
    [ProducesResponseType<PaginatedResult<EventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResult<EventDto>>> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        var targetStakeholders = User.IsInRole(Roles.Student)
            ? "Students"
            : User.IsInRole(Roles.Teacher) ? "Teachers" : null;
        var gradeLevelId = int.TryParse(User.FindFirstValue(PortalClaims.GradeLevelId), out var gradeId)
            ? gradeId
            : (int?)null;
        var sectionId = int.TryParse(User.FindFirstValue(PortalClaims.SectionId), out var section)
            ? section
            : (int?)null;

        return Ok(await ExecuteAsync(() => _events.GetEventsAsync(
            page, pageSize, search, category, status, cancellationToken,
            targetStakeholders, gradeLevelId, sectionId, isAdmin)));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [EndpointSummary("Get an event by id")]
    [ProducesResponseType<EventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> GetEvent(int id, CancellationToken cancellationToken)
    {
        var result = await _events.GetEventAsync(id, cancellationToken);
        return result is null ? NotFoundProblem($"Event {id} was not found.") : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("Create an event")]
    [ProducesResponseType<EventDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EventDto>> CreateEvent(CreateEventDto request, CancellationToken cancellationToken)
    {
        var organizerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(organizerUserId))
        {
            return Unauthorized(new { message = "The authenticated organizer identity is missing." });
        }

        try
        {
            var organizerExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == organizerUserId, cancellationToken);
            if (!organizerExists)
            {
                return Unauthorized(new { message = "The authenticated organizer account no longer exists." });
            }

            var result = await _events.CreateEventAsync(request, organizerUserId, cancellationToken);
            return CreatedAtAction(nameof(GetEvent), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating event: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed to create event.",
                error = "An unexpected error occurred. Check server logs for the full exception details."
            });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("Update an event")]
    [ProducesResponseType<EventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UpdateEvent(int id, UpdateEventDto request, CancellationToken cancellationToken)
    {
        var result = await _events.UpdateEventAsync(id, request, cancellationToken);
        return result is null ? NotFoundProblem($"Event {id} was not found.") : Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("Manually update an event status")]
    [ProducesResponseType<EventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UpdateStatus(int id, UpdateEventStatusDto request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Status))
            return BadRequestProblem("Invalid event status", "A status is required.");

        try
        {
            var result = await _events.UpdateStatusAsync(id, request.Status, cancellationToken);
            return result is null ? NotFoundProblem($"Event {id} was not found.") : Ok(result);
        }
        catch (EventServiceException exception) when (exception.StatusCode == StatusCodes.Status400BadRequest)
        {
            return BadRequestProblem("Invalid event status", exception.Message);
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("Cancel an event")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id, CancellationToken cancellationToken) =>
        await _events.DeleteEventAsync(id, cancellationToken) ? NoContent() : NotFoundProblem($"Event {id} was not found.");

    [HttpPost("{id:int}/register")]
    [Authorize]
    [EndpointSummary("Register a student for an event")]
    [ProducesResponseType<EventRegistrationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventRegistrationDto>> Register(int id, RegisterEventDto request, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequestProblem("Invalid event", "The event id must be greater than zero.");

        if (request is null || request.StudentId <= 0)
            return BadRequestProblem("Invalid student", "A valid studentId is required.");

        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication invalid",
                Detail = "User authentication invalid.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var isAdmin = User.IsAdmin();
        var studentId = isAdmin ? request.StudentId : User.GetStudentId();
        if (studentId is null)
            return ForbiddenProblem("The signed-in account is not linked to an active student.");

        if (!isAdmin && studentId.Value != request.StudentId)
            return ForbiddenProblem("You may only register your own student account.");

        try
        {
            var result = await _events.RegisterAsync(id, studentId.Value, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (EventServiceException exception) when (exception.StatusCode is
            StatusCodes.Status400BadRequest or
            StatusCodes.Status404NotFound or
            StatusCodes.Status409Conflict)
        {
            return StatusCode(exception.StatusCode, new ProblemDetails
            {
                Title = exception.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => "Registration request is invalid",
                    StatusCodes.Status404NotFound => "Registration target was not found",
                    _ => "Registration could not be completed"
                },
                Detail = exception.Message,
                Status = exception.StatusCode
            });
        }
        catch (EventServiceException exception)
        {
            _logger.LogError(exception, "Registration service failed for student {StudentId} and event {EventId}", studentId, id);
            return Problem(
                title: "Registration failed",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Database error registering student {StudentId} for event {EventId}", studentId, id);
            return ConflictProblem("Registration conflict", "The registration could not be saved because the event or student registration changed. Refresh and try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error registering student {StudentId} for event {EventId}", studentId, id);
            return Problem(
                title: "Registration failed",
                detail: "The registration could not be completed. Please try again.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("{id:int}/cancel-registration")]
    [Authorize]
    [EndpointSummary("Cancel a student's event registration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelRegistration(int id, RegisterEventDto request, CancellationToken cancellationToken)
    {
        var studentId = User.IsAdmin() ? request.StudentId : User.GetStudentId();
        if (studentId is null || (!User.IsAdmin() && studentId.Value != request.StudentId))
            return ForbiddenProblem("You may only cancel your own student registration.");
        return await _events.CancelRegistrationAsync(id, studentId.Value, cancellationToken)
            ? NoContent()
            : NotFoundProblem("Active event registration was not found.");
    }

    [HttpGet("{id:int}/registrations")]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("List registrations for an event")]
    [ProducesResponseType<IReadOnlyList<EventRegistrationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EventRegistrationDto>>> GetRegistrations(int id, CancellationToken cancellationToken) =>
        Ok(await _events.GetRegistrationsAsync(id, cancellationToken));

    [HttpGet("{id:int}/comments")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher + "," + Roles.Student)]
    [EndpointSummary("List comments and replies for an event visible to the caller")]
    [ProducesResponseType<IReadOnlyList<EventCommentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EventCommentResponse>>> GetComments(int id, CancellationToken cancellationToken)
    {
        var context = await GetCommentContextAsync(id, cancellationToken);
        if (context is null) return NotFoundProblem("Event was not found or is not visible to this account.");

        var comments = await _db.EventComments
            .AsNoTracking()
            .Where(comment => comment.EventId == id)
            .OrderBy(comment => comment.CreatedAt)
            .Select(comment => new EventCommentResponse(
                comment.Id,
                comment.EventId,
                comment.UserId,
                comment.UserName,
                comment.UserRole,
                comment.ParentCommentId,
                comment.CommentText,
                new DateTimeOffset(DateTime.SpecifyKind(comment.CreatedAt, DateTimeKind.Utc))))
            .ToListAsync(cancellationToken);

        return Ok(comments);
    }

    [HttpPost("{id:int}/comments")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher + "," + Roles.Student)]
    [EndpointSummary("Post a comment on an event visible to the caller")]
    [ProducesResponseType<EventCommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventCommentResponse>> AddComment(
        int id,
        CreateEventCommentRequest request,
        CancellationToken cancellationToken) =>
        await CreateEventCommentAsync(id, request, isAdminReply: false, cancellationToken);

    [HttpPost("{id:int}/comments/reply")]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("Reply as an administrator to a top-level event comment")]
    [ProducesResponseType<EventCommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventCommentResponse>> ReplyToComment(
        int id,
        CreateEventCommentRequest request,
        CancellationToken cancellationToken) =>
        await CreateEventCommentAsync(id, request, isAdminReply: true, cancellationToken);

    private async Task<ActionResult<EventCommentResponse>> CreateEventCommentAsync(
        int eventId,
        CreateEventCommentRequest request,
        bool isAdminReply,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CommentText))
            return BadRequestProblem("Invalid comment", "A comment message is required.");

        if (isAdminReply && request.ParentCommentId is null)
            return BadRequestProblem("Invalid reply", "A parent comment is required for an administrator reply.");

        if (!User.IsInRole(Roles.Admin) && request.ParentCommentId is not null)
            return ForbiddenProblem("Only administrators can reply directly to an event comment.");

        var context = await GetCommentContextAsync(eventId, cancellationToken);
        if (context is null) return NotFoundProblem("Event was not found or is not visible to this account.");

        if (request.ParentCommentId is not null)
        {
            var parentExists = await _db.EventComments.AnyAsync(
                comment => comment.Id == request.ParentCommentId
                    && comment.EventId == eventId
                    && comment.ParentCommentId == null,
                cancellationToken);
            if (!parentExists) return NotFoundProblem("The top-level comment was not found.");
        }

        var comment = new EventComment
        {
            EventId = eventId,
            UserId = context.UserId,
            UserName = context.UserName,
            UserRole = context.UserRole,
            StudentId = context.StudentId,
            ParentCommentId = request.ParentCommentId,
            CommentText = request.CommentText.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        _db.EventComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new EventCommentResponse(
            comment.Id,
            comment.EventId,
            comment.UserId,
            comment.UserName,
            comment.UserRole,
            comment.ParentCommentId,
            comment.CommentText,
            new DateTimeOffset(DateTime.SpecifyKind(comment.CreatedAt, DateTimeKind.Utc)));
        return Created($"/api/events/{eventId}/comments", response);
    }

    [HttpGet("dashboard/stats")]
    [AllowAnonymous]
    [EndpointSummary("Get event dashboard statistics")]
    [ProducesResponseType<EventDashboardStatsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EventDashboardStatsDto>> GetDashboardStats([FromQuery] int count = 5, CancellationToken cancellationToken = default)
    {
        var upcoming = await _events.GetUpcomingEventsAsync(count, cancellationToken);
        var popular = await _events.GetPopularEventsAsync(count, cancellationToken);
        return Ok(new EventDashboardStatsDto
        {
            TotalRegistrations = await _events.GetTotalRegistrationsCountAsync(cancellationToken),
            UpcomingEvents = upcoming,
            PopularEvents = popular
        });
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation) => await operation();

    private async Task<EventCommentContext?> GetCommentContextAsync(int eventId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == userId && account.IsActive, cancellationToken);
        if (user is null) return null;

        var role = User.IsInRole(Roles.Admin)
            ? Roles.Admin
            : User.IsInRole(Roles.Teacher)
                ? Roles.Teacher
                : User.IsInRole(Roles.Student) ? Roles.Student : null;
        if (role is null) return null;

        var calendarEvent = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == eventId && !item.IsDeleted, cancellationToken);
        if (calendarEvent is null) return null;

        int? studentId = null;
        if (role != Roles.Admin)
        {
            if (!calendarEvent.IsPublished || calendarEvent.IsCancelled
                || !TargetRoleIncludes(calendarEvent.TargetRole, role))
                return null;

            if (role == Roles.Student)
            {
                var student = await _db.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserId == userId && item.IsActive, cancellationToken);
                if (student is null || User.GetStudentId() is int claimStudentId && claimStudentId != student.Id)
                    return null;
                if (!CanStudentViewEvent(calendarEvent, student)) return null;
                studentId = student.Id;
            }
            else
            {
                var teacher = await _db.Teachers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserId == userId && item.IsActive, cancellationToken);
                if (teacher is null) return null;
                if (!await CanTeacherViewEventAsync(calendarEvent, teacher.Id, cancellationToken))
                    return null;
            }
        }

        return new EventCommentContext(
            user.Id,
            user.FullName,
            role,
            studentId);
    }

    private async Task<bool> CanTeacherViewEventAsync(Event calendarEvent, int teacherId, CancellationToken cancellationToken)
    {
        if (calendarEvent.GradeLevelId is null
            && calendarEvent.SectionId is null
            && string.IsNullOrWhiteSpace(calendarEvent.TargetGradeIdsCsv)
            && string.IsNullOrWhiteSpace(calendarEvent.TargetSectionIdsCsv))
            return true;

        return await _db.TeacherSubjects.AnyAsync(assignment =>
            assignment.TeacherId == teacherId
            && assignment.IsActive
            && assignment.Subject != null
            && (calendarEvent.SectionId == null || assignment.SectionId == calendarEvent.SectionId)
            && (calendarEvent.GradeLevelId == null || assignment.Subject.GradeLevelId == calendarEvent.GradeLevelId)
            && IncludesId(calendarEvent.TargetGradeIdsCsv, assignment.Subject.GradeLevelId)
            && IncludesId(calendarEvent.TargetSectionIdsCsv, assignment.SectionId),
            cancellationToken);
    }

    private static bool TargetRoleIncludes(string targetRole, string role) =>
        targetRole.Equals("All", StringComparison.OrdinalIgnoreCase)
        || targetRole.Equals(role, StringComparison.OrdinalIgnoreCase)
        || targetRole.Equals($"{role}s", StringComparison.OrdinalIgnoreCase);

    private static bool CanStudentViewEvent(Event calendarEvent, Student student)
    {
        if (calendarEvent.GradeLevelId is not null && calendarEvent.GradeLevelId != student.GradeLevelId) return false;
        if (calendarEvent.SectionId is not null && calendarEvent.SectionId != student.SectionId) return false;
        return IncludesId(calendarEvent.TargetGradeIdsCsv, student.GradeLevelId)
            && IncludesId(calendarEvent.TargetSectionIdsCsv, student.SectionId);
    }

    private static bool IncludesId(string? csv, int id) =>
        string.IsNullOrWhiteSpace(csv)
        || csv.Split(['|', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(value => int.TryParse(value, out var parsedId) && parsedId == id);

    private sealed record EventCommentContext(string UserId, string UserName, string UserRole, int? StudentId);
}