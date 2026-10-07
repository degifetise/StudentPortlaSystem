using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>Read-only access for authenticated guardians to their linked students.</summary>
[ApiController]
[Route("api/parent")]
[Authorize(Roles = Roles.Guardian)]
[Produces("application/json")]
public class ParentController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAttendanceService _attendance;
    private readonly IReportCardService _reportCards;
    private readonly IPdfReportCardService _pdfReportCards;

    public ParentController(
        ApplicationDbContext db,
        IAttendanceService attendance,
        IReportCardService reportCards,
        IPdfReportCardService pdfReportCards)
    {
        _db = db;
        _attendance = attendance;
        _reportCards = reportCards;
        _pdfReportCards = pdfReportCards;
    }

    /// <summary>Lists only the students linked to the authenticated guardian account.</summary>
    [HttpGet("my-students")]
    [ProducesResponseType<IEnumerable<GuardianStudentSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<GuardianStudentSummary>>> GetMyStudents(
        CancellationToken cancellationToken)
    {
        if (!TryGetGuardian(out var userId, out var guardianId))
        {
            return Forbid();
        }

        var students = await _db.StudentGuardians
            .AsNoTracking()
            .Where(link => link.GuardianId == guardianId && link.Guardian!.UserId == userId)
            .Select(link => new GuardianStudentSummary
            {
                StudentId = link.StudentId,
                StudentIdNumber = link.Student!.StudentIdNumber,
                FullName = link.Student.User != null ? link.Student.User.FullName : string.Empty,
                Email = link.Student.User != null ? link.Student.User.Email ?? string.Empty : string.Empty,
                GradeLevelId = link.Student.GradeLevelId,
                GradeLevelName = link.Student.GradeLevel != null ? link.Student.GradeLevel.Name : string.Empty,
                SectionId = link.Student.SectionId,
                SectionName = link.Student.Section != null ? link.Student.Section.Name : string.Empty,
                Relationship = link.Relationship ?? string.Empty
            })
            .OrderBy(student => student.FullName)
            .ToListAsync(cancellationToken);

        return Ok(students);
    }

    /// <summary>Returns attendance records for a student linked to the authenticated guardian.</summary>
    [HttpGet("student/{studentId:int}/attendance")]
    [ProducesResponseType<IEnumerable<AttendanceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AttendanceResponse>>> GetStudentAttendance(
        int studentId,
        CancellationToken cancellationToken)
    {
        if (!await CanReadStudentAsync(studentId, cancellationToken))
        {
            return Forbid();
        }

        var records = await _attendance.ListAsync(studentId, null, null, null, cancellationToken);
        return Ok(records);
    }

    /// <summary>Returns published academic results for a linked student.</summary>
    [HttpGet("student/{studentId:int}/results")]
    [ProducesResponseType<MyResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyResultsResponse>> GetStudentResults(
        int studentId,
        CancellationToken cancellationToken)
    {
        if (!await CanReadStudentAsync(studentId, cancellationToken))
        {
            return Forbid();
        }

        var results = await _reportCards.BuildMyResultsAsync(studentId, cancellationToken);
        return results is null ? NotFound() : Ok(results);
    }

    /// <summary>Downloads the report card PDF for a linked student.</summary>
    [HttpGet("student/{studentId:int}/report-card-pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadStudentReportCard(
        int studentId,
        CancellationToken cancellationToken)
    {
        if (!await CanReadStudentAsync(studentId, cancellationToken))
        {
            return Forbid();
        }

        var pdf = await _pdfReportCards.GenerateAsync(studentId, cancellationToken);
        return pdf is null
            ? NotFound()
            : File(pdf, "application/pdf", $"report-card-{studentId}.pdf");
    }

    private async Task<bool> CanReadStudentAsync(int studentId, CancellationToken cancellationToken)
    {
        if (!TryGetGuardian(out var userId, out var guardianId))
        {
            return false;
        }

        return await _db.StudentGuardians
            .AsNoTracking()
            .AnyAsync(
                link => link.StudentId == studentId
                    && link.GuardianId == guardianId
                    && link.Guardian!.UserId == userId,
                cancellationToken);
    }

    private bool TryGetGuardian(out string userId, out int guardianId)
    {
        userId = User.GetUserId() ?? string.Empty;
        guardianId = User.GetGuardianId() ?? 0;
        return !string.IsNullOrWhiteSpace(userId) && guardianId > 0;
    }
}
