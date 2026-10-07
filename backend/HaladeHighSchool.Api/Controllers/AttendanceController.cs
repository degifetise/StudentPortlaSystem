using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>Daily attendance entry for staff and attendance history for students.</summary>
[ApiController]
[Route("api/attendance")]
[Authorize]
[Produces("application/json")]
public class AttendanceController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAttendanceService _attendance;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        ApplicationDbContext db,
        IAttendanceService attendance,
        ILogger<AttendanceController> logger)
    {
        _db = db;
        _attendance = attendance;
        _logger = logger;
    }

    /// <summary>Returns attendance visible to the current administrator or student.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<AttendanceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AttendanceResponse>>> GetAttendance(
        [FromQuery] int? studentId,
        [FromQuery] int? sectionId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (User.IsAdmin())
        {
            return Ok(await _attendance.ListAsync(studentId, sectionId, from, to, cancellationToken));
        }

        if (User.IsInRole(Roles.Teacher))
        {
            var sectionIds = await GetTeacherSectionIdsAsync(cancellationToken);
            if (sectionIds is null)
            {
                return NotFoundProblem("Your account is not linked to an active teacher profile.");
            }

            if (sectionId is int requestedSection && !sectionIds.Contains(requestedSection))
            {
                return ForbiddenProblem("You may only view attendance for your assigned sections.");
            }

            if (studentId is int requestedStudent && !await _db.Students.AsNoTracking()
                    .AnyAsync(s => s.Id == requestedStudent && sectionIds.Contains(s.SectionId), cancellationToken))
            {
                return ForbiddenProblem("You may only view attendance for students in your assigned sections.");
            }

            return Ok(await _attendance.ListAsync(
                studentId, sectionId, from, to, cancellationToken, sectionIds));
        }

        if (!User.IsInRole(Roles.Student))
        {
            return Forbid();
        }

        var scope = await GetStudentScopeAsync(_db, User.GetUserId(), cancellationToken);
        if (scope is null)
        {
            return ForbiddenProblem("Your account is not linked to an active student profile.");
        }

        if (studentId is not null && studentId != scope.StudentId)
        {
            return ForbiddenProblem("Students may only view their own attendance.");
        }

        return Ok(await _attendance.ListAsync(scope.StudentId, null, from, to, cancellationToken));
    }

    /// <summary>Returns present, absent, late and excused counts for each visible student.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<IEnumerable<AttendanceSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AttendanceSummaryResponse>>> GetSummary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int? studentId,
        [FromQuery] int? sectionId,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            ModelState.AddModelError(nameof(from), "The start date must be on or before the end date.");
            return ValidationProblem(ModelState);
        }

        if (User.IsAdmin())
        {
            return Ok(await _attendance.SummaryAsync(studentId, sectionId, from, to, cancellationToken));
        }

        if (User.IsInRole(Roles.Teacher))
        {
            var sectionIds = await GetTeacherSectionIdsAsync(cancellationToken);
            if (sectionIds is null)
            {
                return NotFoundProblem("Your account is not linked to an active teacher profile.");
            }

            if (sectionId is int requestedSection && !sectionIds.Contains(requestedSection))
            {
                return ForbiddenProblem("You may only view attendance for your assigned sections.");
            }

            if (studentId is int requestedStudent && !await _db.Students.AsNoTracking()
                    .AnyAsync(s => s.Id == requestedStudent && sectionIds.Contains(s.SectionId), cancellationToken))
            {
                return ForbiddenProblem("You may only view attendance for students in your assigned sections.");
            }

            return Ok(await _attendance.SummaryAsync(
                studentId, sectionId, from, to, cancellationToken, sectionIds));
        }

        if (!User.IsInRole(Roles.Student))
        {
            return Forbid();
        }

        var scope = await GetStudentScopeAsync(_db, User.GetUserId(), cancellationToken);
        if (scope is null)
        {
            return ForbiddenProblem("Your account is not linked to an active student profile.");
        }

        if (studentId is not null && studentId != scope.StudentId)
        {
            return ForbiddenProblem("Students may only view their own attendance.");
        }

        return Ok(await _attendance.SummaryAsync(scope.StudentId, null, from, to, cancellationToken));
    }

    /// <summary>Creates or updates one student's attendance record for a day.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<AttendanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceResponse>> MarkAttendance(
        MarkAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var teacherId = await ResolveTeacherIdAsync(cancellationToken);
            if (!User.IsAdmin() && teacherId is null)
            {
                return NotFound(new { message = "Teacher profile is not linked to this user account." });
            }

            var result = await _attendance.MarkAsync(
                request,
                teacherId,
                User.IsAdmin(),
                cancellationToken);

            return result.Succeeded
                ? Ok(result.Items.Single())
                : ToProblem(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save attendance for student {StudentId}", request.StudentId);
            return Problem(title: "Unable to save attendance", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Creates or updates attendance for a whole section on one day.</summary>
    [HttpPost("bulk")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType<IEnumerable<AttendanceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AttendanceResponse>>> BulkMarkAttendance(
        BulkMarkAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var teacherId = await ResolveTeacherIdAsync(cancellationToken);
            if (!User.IsAdmin() && teacherId is null)
            {
                return NotFound(new { message = "Teacher profile is not linked to this user account." });
            }

            var result = await _attendance.BulkMarkAsync(
                request,
                teacherId,
                User.IsAdmin(),
                cancellationToken);

            return result.Succeeded
                ? Ok(result.Items)
                : ToProblem(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save attendance for section {SectionId}", request.SectionId);
            return Problem(title: "Unable to save attendance", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<int?> ResolveTeacherIdAsync(CancellationToken cancellationToken)
    {
        if (User.IsAdmin())
        {
            return null;
        }

        var userId = User.GetUserId();
        return await _db.Teachers
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.IsActive)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<int>?> GetTeacherSectionIdsAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var teacherId = await _db.Teachers
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.IsActive)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (teacherId is null)
        {
            return null;
        }

        return await _db.TeacherSubjects
            .AsNoTracking()
            .Where(ts => ts.TeacherId == teacherId && ts.IsActive)
            .Select(ts => ts.SectionId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private ActionResult ToProblem(AttendanceOperationResult result) =>
        result.NotFound
            ? NotFoundProblem(result.Errors.FirstOrDefault() ?? "Attendance target was not found.")
            : BadRequestProblem("Attendance request rejected", string.Join(" ", result.Errors));
}
