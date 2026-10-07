using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class StudentFeedbackController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;

    public StudentFeedbackController(ApplicationDbContext db) => _db = db;

    [HttpGet("/api/students/feedback")]
    [HttpGet("/api/feedback")]
    [HttpGet("/api/feedback/my-feedback")]
    [Authorize(Roles = Roles.Student)]
    [EndpointSummary("List feedback submitted by the authenticated student")]
    [ProducesResponseType<IReadOnlyList<StudentFeedbackResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudentFeedbackResponse>>> GetMine(CancellationToken cancellationToken)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId is null) return Forbid();

        var rows = await _db.StudentFeedbacks
            .AsNoTracking()
            .Include(feedback => feedback.Student)
                .ThenInclude(student => student.User)
            .Where(feedback => feedback.StudentId == studentId)
            .OrderByDescending(feedback => feedback.SubmittedAt)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpPost("/api/students/feedback")]
    [HttpPost("/api/feedback")]
    [Authorize(Roles = Roles.Student)]
    [EndpointSummary("Submit student feedback")]
    [ProducesResponseType<StudentFeedbackResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudentFeedbackResponse>> Create(
        CreateStudentFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId is null) return Forbid();

        var feedback = new StudentFeedback
        {
            StudentId = studentId.Value,
            Category = request.Category,
            Message = request.Message.Trim(),
            Status = StudentFeedbackStatuses.PendingReview,
            SubmittedAt = DateTime.UtcNow,
        };
        _db.StudentFeedbacks.Add(feedback);
        await _db.SaveChangesAsync(cancellationToken);

        var savedFeedback = await _db.StudentFeedbacks
            .AsNoTracking()
            .Include(item => item.Student)
                .ThenInclude(student => student.User)
            .Where(item => item.Id == feedback.Id)
            .FirstAsync(cancellationToken);

        return Created("/api/feedback", ToResponse(savedFeedback));
    }

    [HttpGet("/api/admin/feedback")]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("List student feedback for administrative review")]
    [ProducesResponseType<IReadOnlyList<StudentFeedbackResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudentFeedbackResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var rows = await _db.StudentFeedbacks
            .AsNoTracking()
            .Include(feedback => feedback.Student)
                .ThenInclude(student => student.User)
            .OrderBy(feedback => feedback.Status == StudentFeedbackStatuses.PendingReview ? 0 : 1)
            .ThenByDescending(feedback => feedback.SubmittedAt)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpPut("/api/admin/feedback/{id:long}/status")]
    [Authorize(Roles = Roles.Admin)]
    [EndpointSummary("Update the review status of student feedback")]
    [ProducesResponseType<StudentFeedbackResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentFeedbackResponse>> UpdateStatus(
        long id,
        UpdateStudentFeedbackStatusRequest request,
        CancellationToken cancellationToken)
    {
        var feedback = await _db.StudentFeedbacks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (feedback is null) return NotFound();

        feedback.Status = request.Status;
        feedback.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var row = await _db.StudentFeedbacks
            .AsNoTracking()
            .Include(item => item.Student)
                .ThenInclude(student => student.User)
            .Where(item => item.Id == id)
            .FirstAsync(cancellationToken);

        return Ok(ToResponse(row));
    }

    private async Task<int?> GetStudentIdAsync(CancellationToken cancellationToken)
    {
        var claimedId = User.GetStudentId();
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _db.Students
            .Where(student => student.UserId == userId && student.IsActive
                && (claimedId == null || student.Id == claimedId))
            .Select(student => (int?)student.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static StudentFeedbackResponse ToResponse(StudentFeedback row) =>
        new(
            row.Id,
            row.StudentId,
            row.Student.StudentIdNumber,
            row.Student.User?.FullName ?? row.Student.StudentIdNumber,
            row.Category,
            row.Message,
            row.Status,
            new DateTimeOffset(DateTime.SpecifyKind(row.SubmittedAt, DateTimeKind.Utc)),
            row.UpdatedAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAt.Value, DateTimeKind.Utc)));
}
