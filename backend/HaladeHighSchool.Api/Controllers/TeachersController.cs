using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>
/// Teacher records and their teaching load. Administrators manage the roster and the
/// timetable; teachers can read their own assigned classes.
/// </summary>
[ApiController]
[Route("api/teachers")]
[Authorize(Roles = Roles.AdminOrTeacher)]
[Produces("application/json")]
public class TeachersController : PortalControllerBase
{
    private const int MaxPageSize = 100;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountProvisioningService _provisioning;
    private readonly IProfilePhotoStorage _photoStorage;
    private readonly ISystemSettingsService _settings;
    private readonly ILogger<TeachersController> _logger;

    public TeachersController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAccountProvisioningService provisioning,
        IProfilePhotoStorage photoStorage,
        ISystemSettingsService settings,
        ILogger<TeachersController> logger)
    {
        _db = db;
        _userManager = userManager;
        _provisioning = provisioning;
        _photoStorage = photoStorage;
        _settings = settings;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Teacher self-service
    // -----------------------------------------------------------------------

    /// <summary>The signed-in teacher's own record, including their assigned classes.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Roles.Teacher)]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TeacherDetailResponse>> GetMe(CancellationToken cancellationToken)
    {
        var teacherId = User.GetTeacherId();
        if (teacherId is null)
        {
            return ForbiddenProblem("Your account is not linked to a teacher profile.");
        }

        var teacher = await ProjectDetail(_db.Teachers.AsNoTracking().Where(t => t.Id == teacherId))
            .FirstOrDefaultAsync(cancellationToken);

        return teacher is null ? NotFoundProblem("Teacher profile not found.") : Ok(teacher);
    }

    /// <summary>The classes (subject and section pairs) the signed-in teacher teaches.</summary>
    [HttpGet("me/classes")]
    [Authorize(Roles = Roles.Teacher)]
    [ProducesResponseType<IEnumerable<TeachingAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TeachingAssignmentResponse>>> GetMyClasses(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return NotFound(new { message = "Teacher profile is not linked to this user account." });
            }

            var teacher = await _db.Teachers
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.IsActive)
                .Select(t => new { t.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (teacher is null)
            {
                return NotFound(new { message = "Teacher profile is not linked to this user account." });
            }

            var classes = await ProjectAssignments(
                    _db.TeacherSubjects.AsNoTracking().Where(ts => ts.TeacherId == teacher.Id && ts.IsActive))
                .OrderBy(a => a.GradeLevelId)
                .ThenBy(a => a.SubjectName)
                .ThenBy(a => a.SectionName)
                .ToListAsync(cancellationToken);

            return Ok(classes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load class roster for section {ClassId}", "me/classes");
            return Problem(
                title: "Unable to load assigned classes",
                detail: "Your assigned classes could not be loaded.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// The class list for one of the signed-in teacher's own assignments, with each student's
    /// weighted standing in that subject.
    ///
    /// Scoped to the assignment rather than to a section on its own: a teacher may see the
    /// students they teach and their marks in the subject they teach them, and nothing else.
    /// </summary>
    [HttpGet("me/classes/{assignmentId:int}/students")]
    [Authorize(Roles = Roles.Teacher)]
    [ProducesResponseType<ClassRosterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassRosterResponse>> GetClassRoster(
        int assignmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await _db.Database.CanConnectAsync(cancellationToken))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Database connection unavailable. Please check SQL Server service."
                });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return NotFound(new { message = "Teacher profile not found" });
            }

            var teacher = await _db.Teachers
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.IsActive)
                .Select(t => new { t.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (teacher is null)
            {
                return NotFound(new { message = "Teacher profile not found" });
            }

            var assignment = await _db.TeacherSubjects
                .AsNoTracking()
                .Where(ts => ts.Id == assignmentId && ts.TeacherId == teacher.Id && ts.IsActive)
                .Select(ts => new
                {
                    ts.Id,
                    ts.SubjectId,
                    SubjectName = ts.Subject != null ? ts.Subject.SubjectName : string.Empty,
                    SubjectCode = ts.Subject != null ? ts.Subject.Code : string.Empty,
                    ts.SectionId,
                    SectionName = ts.Section != null ? ts.Section.Name : string.Empty,
                    GradeLevelId = ts.Subject != null ? ts.Subject.GradeLevelId : 0,
                    GradeLevelName = ts.Subject != null && ts.Subject.GradeLevel != null
                        ? ts.Subject.GradeLevel.Name
                        : string.Empty,
                    ts.AcademicYear
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (assignment is null || assignment.SubjectId == 0 || assignment.SectionId == 0)
            {
                return NotFound(new { message = "Class section not found or access denied" });
            }

            var students = await _db.Students
                .AsNoTracking()
                .Where(s => s.SectionId == assignment.SectionId
                         && s.GradeLevelId == assignment.GradeLevelId)
                .OrderBy(s => s.StudentIdNumber)
                .Select(s => new
                {
                    s.Id,
                    s.StudentIdNumber,
                    FullName = s.User != null ? s.User.FullName : "Unknown",
                    Email = s.User != null ? s.User.Email : null,
                    s.Gender,
                    s.IsActive
                })
                .ToListAsync(cancellationToken);

            if (students.Count == 0)
            {
                return Ok(new ClassRosterResponse
                {
                    AssignmentId = assignment.Id,
                    SubjectId = assignment.SubjectId,
                    SubjectName = assignment.SubjectName,
                    SubjectCode = assignment.SubjectCode,
                    SectionId = assignment.SectionId,
                    SectionName = assignment.SectionName,
                    GradeLevelName = assignment.GradeLevelName,
                    AcademicYear = assignment.AcademicYear,
                    PassMarkPercentage = 50m,
                    Students = []
                });
            }

            var studentIds = students.Select(s => s.Id).ToList();

            var performance = await _db.StudentSubjectPerformances
                .AsNoTracking()
                .Where(p => p.SubjectId == assignment.SubjectId && studentIds.Contains(p.StudentId))
                .Select(p => new
                {
                    p.StudentId,
                    p.QuizScore,
                    p.TestScore,
                    p.MidExamScore,
                    p.FinalExamScore,
                    p.TotalScore,
                    p.IsPassed,
                    p.Status
                })
                .ToListAsync(cancellationToken);

            const decimal passMark = 50m;
            var byStudent = performance
                .GroupBy(p => p.StudentId)
                .ToDictionary(group => group.Key, group => group.First());

            var roster = students
                .Select(s =>
                {
                    if (!byStudent.TryGetValue(s.Id, out var row))
                    {
                        return new ClassRosterEntry
                        {
                            StudentId = s.Id,
                            StudentIdNumber = s.StudentIdNumber,
                            FullName = s.FullName,
                            Email = s.Email,
                            Gender = s.Gender,
                            IsActive = s.IsActive
                        };
                    }

                    var marked = new[]
                    {
                        row.QuizScore, row.TestScore, row.MidExamScore, row.FinalExamScore
                    }.Count(score => score is not null);

                    return new ClassRosterEntry
                    {
                        StudentId = s.Id,
                        StudentIdNumber = s.StudentIdNumber,
                        FullName = s.FullName,
                        Email = s.Email,
                        Gender = s.Gender,
                        IsActive = s.IsActive,
                        TotalScore = row.TotalScore,
                        Status = row.Status,
                        IsPassed = row.IsPassed,
                        ComponentsMarked = marked
                    };
                })
                .ToList();

            var scored = roster.Where(r => r.TotalScore is not null).ToList();

            return Ok(new ClassRosterResponse
            {
                AssignmentId = assignment.Id,
                SubjectId = assignment.SubjectId,
                SubjectName = assignment.SubjectName,
                SubjectCode = assignment.SubjectCode,
                SectionId = assignment.SectionId,
                SectionName = assignment.SectionName,
                GradeLevelName = assignment.GradeLevelName,
                AcademicYear = assignment.AcademicYear,
                PassMarkPercentage = passMark,
                ClassAverage = scored.Count == 0
                    ? null
                    : Math.Round(scored.Average(r => r.TotalScore!.Value), 2),
                MarkedCount = scored.Count,
                PassCount = scored.Count(r => r.IsPassed == true),
                Students = roster
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching students for class section {ClassId}", assignmentId);
            return Problem(
                title: "Unable to load class students",
                detail: "The class roster could not be loaded.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    // -----------------------------------------------------------------------
    // Administration
    // -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<PagedResult<TeacherListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TeacherListItem>>> GetTeachers(
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Teachers.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t =>
                t.EmployeeId.Contains(term) ||
                (t.Specialization != null && t.Specialization.Contains(term)) ||
                (t.User != null && t.User.FullName.Contains(term)) ||
                (t.User != null && t.User.Email != null && t.User.Email.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.EmployeeId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TeacherListItem
            {
                Id = t.Id,
                EmployeeId = t.EmployeeId,
                FullName = t.User != null ? t.User.FullName : string.Empty,
                Email = t.User != null ? t.User.Email : null,
                Specialization = t.Specialization,
                PhoneNumber = t.PhoneNumber,
                IsActive = t.IsActive,
                HasLogin = t.UserId != null,
                AssignmentCount = t.TeacherSubjects.Count(ts => ts.IsActive)
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<TeacherListItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherDetailResponse>> GetTeacher(int id, CancellationToken cancellationToken)
    {
        var teacher = await ProjectDetail(_db.Teachers.AsNoTracking().Where(t => t.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        return teacher is null ? NotFoundProblem($"Teacher {id} was not found.") : Ok(teacher);
    }

    /// <summary>
    /// Hires a teacher, creating the login and the teacher record together. When no password
    /// is supplied a temporary one is generated and returned exactly once.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<CreateTeacherResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<CreateTeacherResponse>> CreateTeacher(
        CreateTeacherRequest request,
        CancellationToken cancellationToken) =>
        CreateTeacherCore(request, null, cancellationToken);

    [HttpPost("with-photo")]
    [Authorize(Roles = Roles.Admin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit((2 * 1024 * 1024) + (64 * 1024))]
    [ProducesResponseType<CreateTeacherResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<CreateTeacherResponse>> CreateTeacherWithPhoto(
        [FromForm] CreateTeacherRequest request,
        CancellationToken cancellationToken) =>
        CreateTeacherCore(request, request.Photo, cancellationToken);

    private async Task<ActionResult<CreateTeacherResponse>> CreateTeacherCore(
        CreateTeacherRequest request,
        IFormFile? photo,
        CancellationToken cancellationToken)
    {
        string? photoUrl = null;
        try
        {
            if (photo is not null)
            {
                photoUrl = await _photoStorage.SaveAsync(photo, cancellationToken);
            }
        }
        catch (InvalidProfilePhotoException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid profile photo",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }

        ProvisionResult<Teacher> result;
        try
        {
            result = await _provisioning.CreateTeacherAsync(new ProvisionTeacherRequest
            {
                Email = request.Email,
                Password = request.Password,
                FullName = request.FullName,
                EmployeeId = request.EmployeeId,
                Specialization = request.Specialization,
                Qualification = request.Qualification,
                PhoneNumber = request.PhoneNumber,
                HireDate = request.HireDate,
                PhotoUrl = photoUrl,
            }, cancellationToken);
        }
        catch
        {
            await _photoStorage.DeleteAsync(photoUrl);
            throw;
        }

        if (!result.Succeeded || result.Entity is null)
        {
            await _photoStorage.DeleteAsync(photoUrl);
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Teacher not created",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["teacher"] = result.Errors.ToArray() }
            });
        }

        var detail = await ProjectDetail(_db.Teachers.AsNoTracking().Where(t => t.Id == result.Entity.Id))
            .FirstAsync(cancellationToken);

        _logger.LogInformation("Admin {Admin} hired teacher {EmployeeId}", User.GetUserId(), detail.EmployeeId);

        return CreatedAtAction(nameof(GetTeacher), new { id = detail.Id }, new CreateTeacherResponse
        {
            Teacher = detail,
            TemporaryPassword = result.TemporaryPassword
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherDetailResponse>> UpdateTeacher(
        int id,
        UpdateTeacherRequest request,
        CancellationToken cancellationToken)
    {
        var teacher = await _db.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (teacher is null)
        {
            return NotFoundProblem($"Teacher {id} was not found.");
        }

        teacher.Specialization = request.Specialization;
        teacher.Qualification = request.Qualification;
        teacher.PhoneNumber = request.PhoneNumber;
        teacher.HireDate = request.HireDate;

        // The display name lives on the Identity user, not on the teacher row.
        if (teacher.User is not null)
        {
            teacher.User.FullName = request.FullName.Trim();
            teacher.User.ProfileImageUrl = request.ProfileImageUrl;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var detail = await ProjectDetail(_db.Teachers.AsNoTracking().Where(t => t.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(detail);
    }

    /// <summary>
    /// Activates or deactivates a teacher. Their login is disabled at the same time, and
    /// their timetable rows are deactivated so they drop out of mark-entry authorisation.
    /// </summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TeacherDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherDetailResponse>> SetStatus(
        int id,
        SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        var teacher = await _db.Teachers
            .Include(t => t.User)
            .Include(t => t.TeacherSubjects)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (teacher is null)
        {
            return NotFoundProblem($"Teacher {id} was not found.");
        }

        teacher.IsActive = request.IsActive;

        if (teacher.User is not null)
        {
            teacher.User.IsActive = request.IsActive;
        }

        foreach (var assignment in teacher.TeacherSubjects)
        {
            assignment.IsActive = request.IsActive;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var detail = await ProjectDetail(_db.Teachers.AsNoTracking().Where(t => t.Id == id))
            .FirstAsync(cancellationToken);

        return Ok(detail);
    }

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ResetPasswordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(
        int id,
        CancellationToken cancellationToken)
    {
        var teacher = await _db.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (teacher is null)
        {
            return NotFoundProblem($"Teacher {id} was not found.");
        }

        if (teacher.UserId is null)
        {
            return BadRequestProblem("No login", "This teacher has no linked account, so there is no password to reset.");
        }

        var user = await _userManager.FindByIdAsync(teacher.UserId);
        if (user is null)
        {
            return NotFoundProblem("The linked account no longer exists.");
        }

        var temporaryPassword = _provisioning.GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Password not reset",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["password"] = result.Errors.Select(e => e.Description).ToArray() }
            });
        }

        _logger.LogInformation(
            "Admin {Admin} reset the password for teacher {EmployeeId}", User.GetUserId(), teacher.EmployeeId);

        return Ok(new ResetPasswordResponse { TemporaryPassword = temporaryPassword });
    }

    /// <summary>
    /// Removes a teacher who has no teaching history. Lessons, assessments and entered marks
    /// all reference the teacher with NO ACTION foreign keys, so those must be reassigned
    /// first; deactivation is the normal route.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteTeacher(int id, CancellationToken cancellationToken)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (teacher is null)
        {
            return NotFoundProblem($"Teacher {id} was not found.");
        }

        var lessons = await _db.Lessons.CountAsync(l => l.TeacherId == id, cancellationToken);
        var assessments = await _db.Assessments.CountAsync(a => a.TeacherId == id, cancellationToken);
        var marks = await _db.Marks.CountAsync(m => m.EnteredByTeacherId == id, cancellationToken);

        if (lessons > 0 || assessments > 0 || marks > 0)
        {
            return ConflictProblem(
                "Teacher has academic records",
                $"This teacher owns {lessons} lesson(s), {assessments} assessment(s) and {marks} entered mark(s). Deactivate the teacher instead of deleting.");
        }

        var userId = teacher.UserId;

        _db.Teachers.Remove(teacher);
        await _db.SaveChangesAsync(cancellationToken);

        if (userId is not null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                await _userManager.DeleteAsync(user);
            }
        }

        _logger.LogWarning("Admin {Admin} deleted teacher {EmployeeId}", User.GetUserId(), teacher.EmployeeId);

        return NoContent();
    }

    // -----------------------------------------------------------------------
    // Timetable
    // -----------------------------------------------------------------------

    /// <summary>Assigns a subject and section to a teacher for the active academic year.</summary>
    [HttpPost("{id:int}/assignments")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TeachingAssignmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeachingAssignmentResponse>> CreateAssignment(
        int id,
        CreateTeachingAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (teacher is null)
        {
            return NotFoundProblem($"Teacher {id} was not found.");
        }

        if (!await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId && s.IsActive, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.SubjectId), "Unknown or inactive subject.");
            return ValidationProblem(ModelState);
        }

        if (!await _db.Sections.AnyAsync(s => s.Id == request.SectionId && s.IsActive, cancellationToken))
        {
            ModelState.AddModelError(nameof(request.SectionId), "Unknown or inactive section.");
            return ValidationProblem(ModelState);
        }

        var academicYear = await _settings.GetAcademicYearAsync(cancellationToken);

        var existing = await _db.TeacherSubjects.FirstOrDefaultAsync(
            ts => ts.TeacherId == id
                  && ts.SubjectId == request.SubjectId
                  && ts.SectionId == request.SectionId
                  && ts.AcademicYear == academicYear,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.IsActive)
            {
                return ConflictProblem(
                    "Already assigned",
                    "This teacher already teaches that subject to that section this academic year.");
            }

            // Re-activate rather than insert, so the unique key is never violated.
            existing.IsActive = true;
            await _db.SaveChangesAsync(cancellationToken);

            var revived = await ProjectAssignments(
                    _db.TeacherSubjects.AsNoTracking().Where(ts => ts.Id == existing.Id))
                .FirstAsync(cancellationToken);

            return CreatedAtAction(nameof(GetTeacher), new { id }, revived);
        }

        var assignment = new TeacherSubject
        {
            TeacherId = id,
            SubjectId = request.SubjectId,
            SectionId = request.SectionId,
            AcademicYear = academicYear,
            IsActive = true,
            AssignedAt = DateTime.UtcNow
        };

        _db.TeacherSubjects.Add(assignment);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await ProjectAssignments(
                _db.TeacherSubjects.AsNoTracking().Where(ts => ts.Id == assignment.Id))
            .FirstAsync(cancellationToken);

        _logger.LogInformation(
            "Assigned {Subject} {Section} to teacher {EmployeeId}",
            created.SubjectCode, created.SectionName, teacher.EmployeeId);

        return CreatedAtAction(nameof(GetTeacher), new { id }, created);
    }

    /// <summary>Removes a teaching assignment, or deactivates it when marks depend on it.</summary>
    [HttpDelete("{id:int}/assignments/{assignmentId:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssignment(
        int id,
        int assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await _db.TeacherSubjects
            .FirstOrDefaultAsync(ts => ts.Id == assignmentId && ts.TeacherId == id, cancellationToken);

        if (assignment is null)
        {
            return NotFoundProblem($"Assignment {assignmentId} was not found for teacher {id}.");
        }

        var hasMarks = await _db.Marks.AnyAsync(
            m => m.EnteredByTeacherId == id && m.SubjectId == assignment.SubjectId, cancellationToken);

        if (hasMarks)
        {
            // Keep the row so the audit trail behind those marks stays intact.
            assignment.IsActive = false;
        }
        else
        {
            _db.TeacherSubjects.Remove(assignment);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Reassigns an active subject/section assignment to another active teacher.</summary>
    [HttpPut("~/api/admin/teacher-assignments/{assignmentId:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TeachingAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeachingAssignmentResponse>> ReassignAssignment(
        int assignmentId,
        ReassignTeachingAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var assignment = await _db.TeacherSubjects
            .FirstOrDefaultAsync(ts => ts.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return NotFoundProblem($"Assignment {assignmentId} was not found.");
        }

        if (!assignment.IsActive)
        {
            return ConflictProblem("Assignment is inactive", "Only active teaching assignments can be reassigned.");
        }

        var teacher = await _db.Teachers
            .AsNoTracking()
            .AnyAsync(t => t.Id == request.TeacherId && t.IsActive, cancellationToken);

        if (!teacher)
        {
            return NotFoundProblem($"Active teacher {request.TeacherId} was not found.");
        }

        var alreadyAssigned = await _db.TeacherSubjects.AnyAsync(
            ts => ts.Id != assignmentId
                && ts.TeacherId == request.TeacherId
                && ts.SubjectId == assignment.SubjectId
                && ts.SectionId == assignment.SectionId
                && ts.AcademicYear == assignment.AcademicYear,
            cancellationToken);

        if (alreadyAssigned)
        {
            return ConflictProblem(
                "Teacher already assigned",
                "The selected teacher already has this subject and section for the academic year.");
        }

        assignment.TeacherId = request.TeacherId;
        await _db.SaveChangesAsync(cancellationToken);

        var response = await ProjectAssignments(
                _db.TeacherSubjects.AsNoTracking().Where(ts => ts.Id == assignmentId))
            .FirstAsync(cancellationToken);

        return Ok(response);
    }

    private static IQueryable<TeacherDetailResponse> ProjectDetail(IQueryable<Teacher> query) =>
        query.Select(t => new TeacherDetailResponse
        {
            Id = t.Id,
            EmployeeId = t.EmployeeId,
            FullName = t.User != null ? t.User.FullName : string.Empty,
            Email = t.User != null ? t.User.Email : null,
            Specialization = t.Specialization,
            PhoneNumber = t.PhoneNumber,
            IsActive = t.IsActive,
            HasLogin = t.UserId != null,
            AssignmentCount = t.TeacherSubjects.Count(ts => ts.IsActive),
            UserId = t.UserId,
            Qualification = t.Qualification,
            HireDate = t.HireDate,
            CreatedAt = t.CreatedAt,
            Assignments = t.TeacherSubjects
                .Where(ts => ts.IsActive)
                .Select(ts => new TeachingAssignmentResponse
                {
                    Id = ts.Id,
                    TeacherId = ts.TeacherId,
                    TeacherName = t.User != null ? t.User.FullName : string.Empty,
                    SubjectId = ts.SubjectId,
                    SubjectName = ts.Subject!.SubjectName,
                    SubjectCode = ts.Subject.Code,
                    GradeLevelId = ts.Subject.GradeLevelId,
                    GradeLevelName = ts.Subject.GradeLevel!.Name,
                    SectionId = ts.SectionId,
                    SectionName = ts.Section!.Name,
                    AcademicYear = ts.AcademicYear,
                    IsActive = ts.IsActive,
                    StudentCount = ts.Section.Students.Count(
                        st => st.IsActive && st.GradeLevelId == ts.Subject.GradeLevelId)
                })
                .ToList()
        });

    private static IQueryable<TeachingAssignmentResponse> ProjectAssignments(IQueryable<TeacherSubject> query) =>
        query.Select(ts => new TeachingAssignmentResponse
        {
            Id = ts.Id,
            TeacherId = ts.TeacherId,
            TeacherName = ts.Teacher!.User != null ? ts.Teacher.User.FullName : string.Empty,
            SubjectId = ts.SubjectId,
            SubjectName = ts.Subject!.SubjectName,
            SubjectCode = ts.Subject.Code,
            GradeLevelId = ts.Subject.GradeLevelId,
            GradeLevelName = ts.Subject.GradeLevel!.Name,
            SectionId = ts.SectionId,
            SectionName = ts.Section!.Name,
            AcademicYear = ts.AcademicYear,
            IsActive = ts.IsActive,
            StudentCount = ts.Section.Students.Count(
                st => st.IsActive && st.GradeLevelId == ts.Subject.GradeLevelId)
        });
}
