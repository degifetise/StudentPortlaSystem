using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>Downloads personal results and role-scoped student, faculty, and section rosters.</summary>
[ApiController]
[Route("api")]
[Produces("application/pdf")]
public sealed class PdfReportsController(IReportPdfService reports) : PortalControllerBase
{
    /// <summary>Downloads the signed-in student's published assessment results.</summary>
    [HttpGet("assessments/my-results/pdf")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadMyResults(CancellationToken cancellationToken)
    {
        var pdf = await reports.GenerateMyResultsAsync(User.GetUserId(), cancellationToken);
        return pdf is null
            ? NotFoundProblem("No active student profile is linked to this account.")
            : PdfFile(pdf, "academic-results.pdf");
    }

    /// <summary>Downloads every enrolled student or the selected grade and section roster.</summary>
    [HttpGet("admin/reports/students/pdf")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadStudentsRoster(
        [FromQuery] int? gradeLevelId,
        [FromQuery] int? sectionId,
        CancellationToken cancellationToken)
    {
        var pdf = await reports.GenerateStudentsRosterAsync(gradeLevelId, sectionId, cancellationToken);
        var filename = sectionId is int section
            ? $"students-section-{section}.pdf"
            : "students-roster.pdf";
        return PdfFile(pdf, filename);
    }

    /// <summary>Downloads the registered teachers and their active assignments.</summary>
    [HttpGet("admin/reports/teachers/pdf")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadTeachersRoster(CancellationToken cancellationToken)
    {
        var pdf = await reports.GenerateTeachersRosterAsync(cancellationToken);
        return PdfFile(pdf, "faculty-directory.pdf");
    }

    /// <summary>Downloads a roster only when the signed-in teacher is assigned to that section.</summary>
    [HttpGet("teacher/reports/section/{sectionId:int}/pdf")]
    [Authorize(Roles = Roles.Teacher)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadTeacherSectionRoster(
        int sectionId,
        CancellationToken cancellationToken)
    {
        var pdf = await reports.GenerateTeacherSectionRosterAsync(
            User.GetUserId(),
            sectionId,
            cancellationToken);

        return pdf is null
            ? NotFoundProblem("No active teaching assignment was found for this section.")
            : PdfFile(pdf, $"section-{sectionId}-roster.pdf");
    }

    private static FileStreamResult PdfFile(byte[] pdf, string filename) =>
        new(new MemoryStream(pdf, writable: false), "application/pdf")
        {
            FileDownloadName = filename
        };
}
