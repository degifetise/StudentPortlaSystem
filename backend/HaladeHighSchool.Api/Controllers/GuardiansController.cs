using System.ComponentModel.DataAnnotations;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

[ApiController]
[Route("api/guardians")]
[Produces("application/json")]
public class GuardiansController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountProvisioningService _provisioning;
    private readonly ILogger<GuardiansController> _logger;

    public GuardiansController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAccountProvisioningService provisioning,
        ILogger<GuardiansController> logger)
    {
        _db = db;
        _userManager = userManager;
        _provisioning = provisioning;
        _logger = logger;
    }

    [HttpGet("me")]
    [Authorize(Roles = Roles.Guardian)]
    [ProducesResponseType<GuardianDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GuardianDetailResponse>> GetMe(CancellationToken cancellationToken)
    {
        var guardianId = User.GetGuardianId();
        if (guardianId is null)
        {
            return ForbiddenProblem("Your account is not linked to a guardian profile.");
        }

        var guardian = await _db.Guardians
            .AsNoTracking()
            .Where(g => g.Id == guardianId.Value)
            .Select(g => new GuardianDetailResponse
            {
                GuardianId = g.Id,
                FullName = g.User != null ? g.User.FullName : string.Empty,
                Email = g.User != null ? g.User.Email ?? string.Empty : string.Empty,
                PhoneNumber = g.PhoneNumber,
                CreatedAt = g.CreatedAt,
                LinkedStudents = g.StudentGuardians
                    .Where(sg => sg.Student != null)
                    .Select(sg => new GuardianStudentSummary
                    {
                        StudentId = sg.StudentId,
                        StudentIdNumber = sg.Student!.StudentIdNumber,
                        FullName = sg.Student.User != null ? sg.Student.User.FullName : string.Empty,
                        Email = sg.Student.User != null ? sg.Student.User.Email ?? string.Empty : string.Empty,
                        GradeLevelId = sg.Student.GradeLevelId,
                        GradeLevelName = sg.Student.GradeLevel != null ? sg.Student.GradeLevel.Name : string.Empty,
                        SectionId = sg.Student.SectionId,
                        SectionName = sg.Student.Section != null ? sg.Student.Section.Name : string.Empty,
                        Relationship = sg.Relationship ?? string.Empty
                    })
                    .OrderBy(s => s.StudentIdNumber)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return guardian is null ? NotFoundProblem("Guardian profile not found.") : Ok(guardian);
    }

    [HttpGet("students")]
    [Authorize(Roles = Roles.Guardian)]
    [ProducesResponseType<IEnumerable<GuardianStudentSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GuardianStudentSummary>>> GetLinkedStudents(CancellationToken cancellationToken)
    {
        var guardianId = User.GetGuardianId();
        if (guardianId is null)
        {
            return ForbiddenProblem("Your account is not linked to a guardian profile.");
        }

        var students = await _db.StudentGuardians
            .AsNoTracking()
            .Where(sg => sg.GuardianId == guardianId.Value)
            .Select(sg => new GuardianStudentSummary
            {
                StudentId = sg.StudentId,
                StudentIdNumber = sg.Student!.StudentIdNumber,
                FullName = sg.Student.User != null ? sg.Student.User.FullName : string.Empty,
                Email = sg.Student.User != null ? sg.Student.User.Email ?? string.Empty : string.Empty,
                GradeLevelId = sg.Student.GradeLevelId,
                GradeLevelName = sg.Student.GradeLevel != null ? sg.Student.GradeLevel.Name : string.Empty,
                SectionId = sg.Student.SectionId,
                SectionName = sg.Student.Section != null ? sg.Student.Section.Name : string.Empty,
                Relationship = sg.Relationship ?? string.Empty
            })
            .OrderBy(s => s.StudentIdNumber)
            .ToListAsync(cancellationToken);

        return Ok(students);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<GuardianLinkResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuardianLinkResponse>> CreateGuardian(
        CreateGuardianRequest request,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.Id == request.StudentId && s.IsActive)
            .Select(s => new { s.Id, s.UserId, s.StudentIdNumber, UserFullName = s.User != null ? s.User.FullName : string.Empty })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return BadRequestProblem("Student not found", "The selected student does not exist or is inactive.");
        }

        var existingLink = await _db.StudentGuardians
            .AsNoTracking()
            .AnyAsync(
                sg => sg.StudentId == request.StudentId
                    && sg.Guardian != null
                    && sg.Guardian.User != null
                    && sg.Guardian.User.Email != null
                    && sg.Guardian.User.Email == request.Email,
                cancellationToken);

        if (existingLink)
        {
            return BadRequestProblem("Duplicate guardian", "This guardian is already linked to the selected student.");
        }

        var result = await _provisioning.CreateGuardianAsync(new ProvisionGuardianRequest
        {
            Email = request.Email,
            Password = request.Password,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            StudentId = request.StudentId,
            Relationship = request.Relationship
        }, cancellationToken);

        if (!result.Succeeded || result.Entity is null)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Guardian not created",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["guardian"] = result.Errors.ToArray() }
            });
        }

        var guardian = await _db.Guardians
            .AsNoTracking()
            .Where(g => g.Id == result.Entity.GuardianId)
            .Select(g => new { g.Id, UserFullName = g.User != null ? g.User.FullName : string.Empty, UserEmail = g.User != null ? g.User.Email ?? string.Empty : string.Empty })
            .FirstAsync(cancellationToken);

        var response = new GuardianLinkResponse
        {
            GuardianId = guardian.Id,
            StudentId = request.StudentId,
            FullName = guardian.UserFullName,
            Email = guardian.UserEmail,
            Relationship = request.Relationship,
            IsPrimaryContact = result.Entity.IsPrimaryContact,
            CreatedAt = result.Entity.CreatedAt
        };

        _logger.LogInformation("Admin {Admin} created guardian {GuardianId} for student {StudentId}", User.GetUserId(), guardian.Id, request.StudentId);

        return CreatedAtAction(nameof(GetMe), new { }, response);
    }

    [HttpPost("{studentId:int}/link")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<GuardianLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuardianLinkResponse>> LinkGuardianToStudent(
        int studentId,
        [FromBody] LinkGuardianRequest request,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.IsActive, cancellationToken);

        if (student is null)
        {
            return NotFoundProblem($"Student {studentId} was not found.");
        }

        var guardian = await _db.Guardians
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == request.GuardianId, cancellationToken);

        if (guardian is null)
        {
            return NotFoundProblem($"Guardian {request.GuardianId} was not found.");
        }

        if (await _db.StudentGuardians.AnyAsync(sg => sg.StudentId == studentId && sg.GuardianId == request.GuardianId, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Guardian already linked",
                Detail = "This guardian is already linked to the selected student.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var link = new StudentGuardian
        {
            StudentId = studentId,
            GuardianId = request.GuardianId,
            Relationship = request.Relationship,
            IsPrimaryContact = request.IsPrimaryContact,
            CreatedAt = DateTime.UtcNow
        };

        _db.StudentGuardians.Add(link);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new GuardianLinkResponse
        {
            GuardianId = guardian.Id,
            StudentId = studentId,
            FullName = guardian.User?.FullName ?? string.Empty,
            Email = guardian.User?.Email ?? string.Empty,
            Relationship = request.Relationship,
            IsPrimaryContact = request.IsPrimaryContact,
            CreatedAt = link.CreatedAt
        });
    }
}

public record LinkGuardianRequest
{
    [Required]
    public int GuardianId { get; init; }

    [MaxLength(50)]
    public string? Relationship { get; init; }

    public bool IsPrimaryContact { get; init; }
}
