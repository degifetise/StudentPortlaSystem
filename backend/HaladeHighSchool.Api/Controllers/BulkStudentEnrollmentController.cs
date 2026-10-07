using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

[ApiController]
[Route("api/students/bulk-enroll")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class BulkStudentEnrollmentController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;

    public BulkStudentEnrollmentController(ApplicationDbContext db) => _db = db;

    [HttpPost]
    [EndpointSummary("Enroll multiple students in a grade and section")]
    [ProducesResponseType<BulkStudentEnrollmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkStudentEnrollmentResponse>> Enroll(
        BulkStudentEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var gradeExists = await _db.GradeLevels.AnyAsync(
            grade => grade.Id == request.GradeLevelId && grade.IsActive, cancellationToken);
        var section = await _db.Sections
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.SectionId && item.IsActive, cancellationToken);
        if (!gradeExists || section is null)
            return BadRequestProblem("Invalid destination", "Choose an active grade and section.");

        var requestedNumbers = request.StudentIdNumbers
            .Select(number => number.Trim())
            .Where(number => number.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requestedNumbers.Length == 0)
            return BadRequestProblem("No students selected", "Enter at least one student ID number.");

        var students = await _db.Students
            .Where(student => requestedNumbers.Contains(student.StudentIdNumber) && student.IsActive)
            .ToListAsync(cancellationToken);
        if (students.Count != requestedNumbers.Length)
        {
            var foundNumbers = students.Select(student => student.StudentIdNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingNumbers = requestedNumbers.Where(number => !foundNumbers.Contains(number));
            return BadRequestProblem("Students not found", $"No active student matches: {string.Join(", ", missingNumbers)}.");
        }

        var currentOccupancy = await _db.Students.CountAsync(
            student => student.IsActive
                && student.GradeLevelId == request.GradeLevelId
                && student.SectionId == request.SectionId,
            cancellationToken);
        var studentsAlreadyInDestination = students.Count(student =>
            student.GradeLevelId == request.GradeLevelId && student.SectionId == request.SectionId);
        if (currentOccupancy + students.Count - studentsAlreadyInDestination > section.Capacity)
            return ConflictProblem("Section is full", "The requested enrollment would exceed the section capacity.");

        foreach (var student in students)
        {
            student.GradeLevelId = request.GradeLevelId;
            student.SectionId = request.SectionId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new BulkStudentEnrollmentResponse(
            students.Count,
            students.Select(student => student.StudentIdNumber).ToList()));
    }
}
