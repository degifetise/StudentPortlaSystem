using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>Administrator-only guardian account and student-link management.</summary>
[ApiController]
[Route("api/admin/guardians")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class AdminGuardiansController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAccountProvisioningService _provisioning;

    public AdminGuardiansController(ApplicationDbContext db, IAccountProvisioningService provisioning)
    {
        _db = db;
        _provisioning = provisioning;
    }

    /// <summary>Lists guardian accounts together with their linked students.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminGuardianResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminGuardianResponse>>> GetGuardians(
        CancellationToken cancellationToken)
    {
        var guardians = await GuardianQuery()
            .OrderBy(g => g.User!.FullName)
            .ToListAsync(cancellationToken);

        return Ok(guardians.Select(ToResponse).ToList());
    }

    /// <summary>Gets one guardian account and its linked students.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminGuardianResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminGuardianResponse>> GetGuardian(
        int id,
        CancellationToken cancellationToken)
    {
        var guardian = await GuardianQuery().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return guardian is null ? NotFoundProblem($"Guardian {id} was not found.") : Ok(ToResponse(guardian));
    }

    /// <summary>Creates a guardian account. The generated temporary password is returned once.</summary>
    [HttpPost("~/api/admin/register-parent")]
    [ProducesResponseType<RegisteredGuardianResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisteredGuardianResponse>> RegisterParent(
        RegisterGuardianRequest request,
        CancellationToken cancellationToken)
    {
        var fullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim();
        var result = await _provisioning.CreateGuardianAccountAsync(
            new ProvisionGuardianAccountRequest
            {
                Email = request.Email.Trim(),
                FullName = fullName,
                PhoneNumber = request.PhoneNumber
            },
            cancellationToken);

        if (!result.Succeeded || result.Entity is null)
        {
            var isConflict = result.Errors.Any(error =>
                error.Contains("already registered", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("already taken", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("already in use", StringComparison.OrdinalIgnoreCase));
            if (isConflict)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Email already registered",
                    Detail = "An account already uses this email address.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            return BadRequest(new ValidationProblemDetails
            {
                Title = "Guardian not created",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["guardian"] = result.Errors.ToArray() }
            });
        }

        var response = new RegisteredGuardianResponse
        {
            GuardianId = result.Entity.Id,
            FullName = fullName,
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            TemporaryPassword = result.TemporaryPassword
        };

        return CreatedAtAction(nameof(GetGuardian), new { id = result.Entity.Id }, response);
    }

    /// <summary>Links an existing guardian to a student.</summary>
    [HttpPost("link-student")]
    [ProducesResponseType<GuardianLinkResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GuardianLinkResponse>> LinkStudent(
        LinkGuardianStudentRequest request,
        CancellationToken cancellationToken)
    {
        var guardian = await _db.Guardians
            .AsNoTracking()
            .Where(g => g.Id == request.GuardianId)
            .Select(g => new
            {
                g.Id,
                FullName = g.User != null ? g.User.FullName : string.Empty,
                Email = g.User != null ? g.User.Email ?? string.Empty : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (guardian is null)
        {
            return NotFoundProblem($"Guardian {request.GuardianId} was not found.");
        }

        var studentExists = await _db.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.StudentId && s.IsActive, cancellationToken);
        if (!studentExists)
        {
            return NotFoundProblem($"Student {request.StudentId} was not found or is inactive.");
        }

        var link = new StudentGuardian
        {
            GuardianId = request.GuardianId,
            StudentId = request.StudentId,
            Relationship = request.Relationship.Trim(),
            IsPrimaryContact = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.StudentGuardians.Add(link);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            return Conflict(new ProblemDetails
            {
                Title = "Guardian already linked",
                Detail = "This guardian is already linked to the selected student.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var response = new GuardianLinkResponse
        {
            GuardianId = guardian.Id,
            StudentId = request.StudentId,
            FullName = guardian.FullName,
            Email = guardian.Email,
            Relationship = link.Relationship,
            IsPrimaryContact = link.IsPrimaryContact,
            CreatedAt = link.CreatedAt
        };

        return CreatedAtAction(nameof(GetGuardian), new { id = guardian.Id }, response);
    }

    /// <summary>Updates the relationship recorded for an existing guardian/student link.</summary>
    [HttpPut("update-link")]
    [ProducesResponseType<GuardianLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuardianLinkResponse>> UpdateLink(
        UpdateGuardianLinkRequest request,
        CancellationToken cancellationToken)
    {
        var link = await _db.StudentGuardians
            .Include(sg => sg.Guardian)
                .ThenInclude(guardian => guardian!.User)
            .FirstOrDefaultAsync(
                sg => sg.GuardianId == request.GuardianId && sg.StudentId == request.StudentId,
                cancellationToken);

        if (link is null)
        {
            return NotFoundProblem($"Guardian {request.GuardianId} is not linked to student {request.StudentId}.");
        }

        link.Relationship = request.Relationship.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new GuardianLinkResponse
        {
            GuardianId = link.GuardianId,
            StudentId = link.StudentId,
            FullName = link.Guardian?.User?.FullName ?? string.Empty,
            Email = link.Guardian?.User?.Email ?? string.Empty,
            Relationship = link.Relationship,
            IsPrimaryContact = link.IsPrimaryContact,
            CreatedAt = link.CreatedAt
        });
    }

    /// <summary>Unlinks a guardian from a student, revoking the guardian's access to that student's data.</summary>
    [HttpDelete("{guardianId:int}/students/{studentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UnlinkStudent(
        int guardianId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var link = await _db.StudentGuardians
            .FirstOrDefaultAsync(
                sg => sg.GuardianId == guardianId && sg.StudentId == studentId,
                cancellationToken);

        if (link is null)
        {
            return NotFoundProblem($"Guardian {guardianId} is not linked to student {studentId}.");
        }

        _db.StudentGuardians.Remove(link);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private IQueryable<Guardian> GuardianQuery() =>
        _db.Guardians
            .AsNoTracking()
            .Include(g => g.User)
            .Include(g => g.StudentGuardians)
                .ThenInclude(link => link.Student)
                    .ThenInclude(student => student!.User)
            .Include(g => g.StudentGuardians)
                .ThenInclude(link => link.Student)
                    .ThenInclude(student => student!.GradeLevel)
            .Include(g => g.StudentGuardians)
                .ThenInclude(link => link.Student)
                    .ThenInclude(student => student!.Section);

    private static AdminGuardianResponse ToResponse(Guardian guardian) =>
        new()
        {
            GuardianId = guardian.Id,
            FullName = guardian.User?.FullName ?? string.Empty,
            Email = guardian.User?.Email ?? string.Empty,
            PhoneNumber = guardian.PhoneNumber,
            LinkedStudents = guardian.StudentGuardians
                .Where(link => link.Student is not null)
                .Select(link => new AdminGuardianStudentResponse
                {
                    StudentId = link.StudentId,
                    StudentIdNumber = link.Student!.StudentIdNumber,
                    FullName = link.Student.User?.FullName ?? string.Empty,
                    Email = link.Student.User?.Email ?? string.Empty,
                    GradeLevelName = link.Student.GradeLevel?.Name ?? string.Empty,
                    SectionName = link.Student.Section?.Name ?? string.Empty,
                    Relationship = link.Relationship ?? string.Empty
                })
                .OrderBy(student => student.FullName)
                .ToList()
        };
}
