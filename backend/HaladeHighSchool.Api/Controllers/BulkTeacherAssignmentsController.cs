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
[Route("api/allocations")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class BulkTeacherAssignmentsController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISystemSettingsService _settings;

    public BulkTeacherAssignmentsController(ApplicationDbContext db, ISystemSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpPost("assign-bulk")]
    [EndpointSummary("Assign a teacher to multiple subjects and sections")]
    [ProducesResponseType<BulkTeacherAssignmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkTeacherAssignmentResponse>> AssignBulk(
        BulkTeacherAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var teacherExists = await _db.Teachers.AnyAsync(teacher => teacher.Id == request.TeacherId, cancellationToken);
        if (!teacherExists) return NotFoundProblem($"Teacher {request.TeacherId} was not found.");

        var subjectIds = request.SubjectIds.Distinct().ToArray();
        var sectionIds = request.SectionIds.Distinct().ToArray();
        var activeSubjects = await _db.Subjects
            .Where(subject => subjectIds.Contains(subject.Id) && subject.IsActive)
            .Select(subject => subject.Id)
            .ToListAsync(cancellationToken);
        var activeSections = await _db.Sections
            .Where(section => sectionIds.Contains(section.Id) && section.IsActive)
            .Select(section => section.Id)
            .ToListAsync(cancellationToken);
        if (activeSubjects.Count != subjectIds.Length || activeSections.Count != sectionIds.Length)
            return BadRequestProblem("Invalid assignments", "Every selected subject and section must exist and be active.");

        var academicYear = await _settings.GetAcademicYearAsync(cancellationToken);
        var existing = await _db.TeacherSubjects
            .Where(assignment => assignment.TeacherId == request.TeacherId
                && assignment.AcademicYear == academicYear
                && subjectIds.Contains(assignment.SubjectId)
                && sectionIds.Contains(assignment.SectionId))
            .ToListAsync(cancellationToken);
        var existingKeys = existing.ToDictionary(
            assignment => (assignment.SubjectId, assignment.SectionId));
        var created = 0;
        var reactivated = 0;
        var alreadyActive = 0;

        foreach (var subjectId in subjectIds)
        {
            foreach (var sectionId in sectionIds)
            {
                if (existingKeys.TryGetValue((subjectId, sectionId), out var assignment))
                {
                    if (assignment.IsActive) alreadyActive++;
                    else
                    {
                        assignment.IsActive = true;
                        reactivated++;
                    }
                    continue;
                }

                _db.TeacherSubjects.Add(new TeacherSubject
                {
                    TeacherId = request.TeacherId,
                    SubjectId = subjectId,
                    SectionId = sectionId,
                    AcademicYear = academicYear,
                    IsActive = true,
                    AssignedAt = DateTime.UtcNow,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        var response = new BulkTeacherAssignmentResponse(created, reactivated, alreadyActive);
        return Created("/api/allocations/assign-bulk", response);
    }
}
