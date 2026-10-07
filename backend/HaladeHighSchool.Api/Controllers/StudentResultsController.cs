using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// A student's own results. Separate from StudentsController, which is administrative: the two
/// share the api/students prefix but not their audience, and combining them would mean either
/// admin-only actions on a student route or a student-reachable admin controller.
/// </summary>
[ApiController]
[Route("api/students")]
[Authorize(Roles = Roles.Student)]
[Produces("application/json")]
public class StudentResultsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IReportCardService _reportCards;
    private readonly ILogger<StudentResultsController> _logger;

    public StudentResultsController(
        ApplicationDbContext db,
        IReportCardService reportCards,
        ILogger<StudentResultsController> logger)
    {
        _db = db;
        _reportCards = reportCards;
        _logger = logger;
    }

    /// <summary>
    /// Subjects, component scores, weighted totals and grade summary for the signed-in student.
    ///
    /// The student is taken from the token's student claim, so one student cannot read another's
    /// results by changing a parameter - there is no parameter to change. Only published marks
    /// reach the view this is built from.
    /// </summary>
    [HttpGet("my-results")]
    [ProducesResponseType<MyResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyResultsResponse>> GetMyResults(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return NotFound(new { message = "Student profile record not found." });
            }

            var student = await _db.Students
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => new { s.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (student is null)
            {
                return NotFound(new { message = "Student profile record not found." });
            }

            var results = await _reportCards.BuildMyResultsAsync(student.Id, cancellationToken);
            return results is null
                ? NotFound(new { message = "Student profile record not found." })
                : Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading academic results for student user {UserId}", userId);
            return Problem(
                title: "Academic results unavailable",
                detail: "Your results are currently being processed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Published Grade 11-12 subject history with term and cumulative GPA.</summary>
    [HttpGet("my-transcript")]
    [ProducesResponseType<TranscriptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptResponse>> GetMyTranscript(CancellationToken cancellationToken)
    {
        var studentId = User.GetStudentId();
        if (studentId is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "No student profile",
                Detail = "This account is not linked to a student record. Contact the school office.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var transcript = await _reportCards.BuildTranscriptAsync(studentId.Value, cancellationToken);
        return transcript is null ? NotFound() : Ok(transcript);
    }
}
