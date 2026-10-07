using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaladeHighSchool.Api.Services;

public record ProvisionStudentRequest
{
    /// <summary>Leave blank to generate a compliant temporary password.</summary>
    public string? Password { get; init; }
    public string FullName { get; init; } = string.Empty;
    public int GradeLevelId { get; init; }
    public int SectionId { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? Address { get; init; }
    public string? PhotoUrl { get; init; }
}

public record ProvisionTeacherRequest
{
    public string Email { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? EmployeeId { get; init; }
    public string? Specialization { get; init; }
    public string? Qualification { get; init; }
    public string? PhoneNumber { get; init; }
    public DateOnly? HireDate { get; init; }
    public string? PhotoUrl { get; init; }
}

public record ProvisionStaffRequest
{
    public string Email { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
}

public record ProvisionGuardianRequest
{
    public string Email { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public int StudentId { get; init; }
    public string? Relationship { get; init; }
}

public record ProvisionGuardianAccountRequest
{
    public string Email { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}

public record ProvisionResult<T> where T : class
{
    public bool Succeeded { get; init; }
    public T? Entity { get; init; }

    /// <summary>Set only when this service generated the password.</summary>
    public string? TemporaryPassword { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static ProvisionResult<T> Fail(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };

    public static ProvisionResult<T> Ok(T entity, string? temporaryPassword) =>
        new() { Succeeded = true, Entity = entity, TemporaryPassword = temporaryPassword };
}

public interface IAccountProvisioningService
{
    /// <summary>
    /// Creates the Identity login and the Students row together. Both are written in one
    /// transaction so a failure never leaves an account without a student profile.
    /// </summary>
    /// <param name="onCreated">Optional related state to persist in the same transaction.</param>
    Task<ProvisionResult<Student>> CreateStudentAsync(
        ProvisionStudentRequest request,
        CancellationToken cancellationToken = default,
        Func<Student, string, CancellationToken, Task>? onCreated = null);

    /// <summary>Creates the Identity login and the Teachers row together.</summary>
    Task<ProvisionResult<Teacher>> CreateTeacherAsync(
        ProvisionTeacherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a staff login and profile.</summary>
    Task<ProvisionResult<ApplicationUser>> CreateStaffAsync(
        ProvisionStaffRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates the guardian login and links the guardian to the student in one transaction.</summary>
    Task<ProvisionResult<StudentGuardian>> CreateGuardianAsync(
        ProvisionGuardianRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a guardian login and profile without requiring a student link.</summary>
    Task<ProvisionResult<Guardian>> CreateGuardianAccountAsync(
        ProvisionGuardianAccountRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Generates a password that satisfies the configured Identity rules.</summary>
    string GenerateTemporaryPassword();
}

public class AccountProvisioningService : IAccountProvisioningService
{
    private const string PasswordUpper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string PasswordLower = "abcdefghijkmnopqrstuvwxyz";
    private const string PasswordDigits = "23456789";
    private const string PasswordSymbols = "!@#$%*?";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ProvisioningSettings _provisioning;
    private readonly ILogger<AccountProvisioningService> _logger;

    public AccountProvisioningService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IOptions<ProvisioningSettings> provisioning,
        ILogger<AccountProvisioningService> logger)
    {
        _db = db;
        _userManager = userManager;
        _provisioning = provisioning.Value;
        _logger = logger;
    }

    public async Task<ProvisionResult<Student>> CreateStudentAsync(
        ProvisionStudentRequest request,
        CancellationToken cancellationToken = default,
        Func<Student, string, CancellationToken, Task>? onCreated = null)
    {
        var gradeLevel = await _db.GradeLevels
            .FirstOrDefaultAsync(g => g.Id == request.GradeLevelId, cancellationToken);

        if (gradeLevel is null || !gradeLevel.IsActive)
        {
            return ProvisionResult<Student>.Fail("The selected grade level does not exist or is inactive.");
        }

        var section = await _db.Sections
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken);

        if (section is null || !section.IsActive)
        {
            return ProvisionResult<Student>.Fail("The selected section does not exist or is inactive.");
        }

        var occupancy = await _db.Students.CountAsync(
            s => s.GradeLevelId == request.GradeLevelId
              && s.SectionId == request.SectionId
              && s.IsActive,
            cancellationToken);

        if (occupancy >= section.Capacity)
        {
            return ProvisionResult<Student>.Fail(
                $"{gradeLevel.Name} {section.Name} is full ({occupancy}/{section.Capacity}).");
        }

        var generatedPassword = string.IsNullOrWhiteSpace(request.Password)
            ? GenerateTemporaryPassword()
            : null;

        return await ExecuteInTransactionAsync(async () =>
        {
            var studentIdNumber = await GenerateSequenceAsync("sms", cancellationToken);
            var email = $"{studentIdNumber}@{_provisioning.StudentEmailDomain}".ToLowerInvariant();

            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                return ProvisionResult<Student>.Fail($"The generated email '{email}' is already registered.");
            }

            var userResult = await CreateUserAsync(
                email,
                request.FullName,
                generatedPassword ?? request.Password!,
                Roles.Student,
                request.PhotoUrl);

            if (userResult.User is null)
            {
                return ProvisionResult<Student>.Fail(userResult.Errors);
            }

            var student = new Student
            {
                StudentIdNumber = studentIdNumber,
                GradeLevelId = request.GradeLevelId,
                SectionId = request.SectionId,
                UserId = userResult.User.Id,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                GuardianName = request.GuardianName,
                GuardianPhone = request.GuardianPhone,
                Address = request.Address,
                EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Students.Add(student);
            await _db.SaveChangesAsync(cancellationToken);
            await LogPhotoPersistenceAsync(userResult.User, cancellationToken);

            // Student approval state must commit or roll back with the new account.
            if (onCreated is not null)
            {
                await onCreated(student, email, cancellationToken);
            }

            return ProvisionResult<Student>.Ok(student, generatedPassword);
        }, "student account", cancellationToken, IsolationLevel.Serializable);
    }

    public async Task<ProvisionResult<Teacher>> CreateTeacherAsync(
        ProvisionTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return ProvisionResult<Teacher>.Fail($"The email '{request.Email}' is already registered.");
        }

        var employeeId = string.IsNullOrWhiteSpace(request.EmployeeId)
            ? await GenerateSequenceAsync("EMP", cancellationToken)
            : request.EmployeeId.Trim();

        if (await _db.Teachers.AnyAsync(t => t.EmployeeId == employeeId, cancellationToken))
        {
            return ProvisionResult<Teacher>.Fail($"Employee ID '{employeeId}' is already in use.");
        }

        var generatedPassword = string.IsNullOrWhiteSpace(request.Password)
            ? GenerateTemporaryPassword()
            : null;

        return await ExecuteInTransactionAsync(async () =>
        {
            var userResult = await CreateUserAsync(
                request.Email,
                request.FullName,
                generatedPassword ?? request.Password!,
                Roles.Teacher,
                request.PhotoUrl);

            if (userResult.User is null)
            {
                return ProvisionResult<Teacher>.Fail(userResult.Errors);
            }

            var teacher = new Teacher
            {
                EmployeeId = employeeId,
                Specialization = request.Specialization,
                Qualification = request.Qualification,
                PhoneNumber = request.PhoneNumber,
                HireDate = request.HireDate,
                UserId = userResult.User.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Teachers.Add(teacher);
            await _db.SaveChangesAsync(cancellationToken);
            await LogPhotoPersistenceAsync(userResult.User, cancellationToken);

            return ProvisionResult<Teacher>.Ok(teacher, generatedPassword);
        }, request.Email, cancellationToken);
    }

    public async Task<ProvisionResult<ApplicationUser>> CreateStaffAsync(
        ProvisionStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var fullName = request.FullName.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return ProvisionResult<ApplicationUser>.Fail($"The email '{email}' is already registered.");
        }

        var generatedPassword = string.IsNullOrWhiteSpace(request.Password)
            ? GenerateTemporaryPassword()
            : null;

        return await ExecuteInTransactionAsync(async () =>
        {
            var userResult = await CreateUserAsync(
                email,
                fullName,
                generatedPassword ?? request.Password!,
                Roles.Staff,
                request.PhotoUrl);

            if (userResult.User is null)
            {
                return ProvisionResult<ApplicationUser>.Fail(userResult.Errors);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await LogPhotoPersistenceAsync(userResult.User, cancellationToken);

            return ProvisionResult<ApplicationUser>.Ok(userResult.User, generatedPassword);
        }, email, cancellationToken);
    }

    public async Task<ProvisionResult<StudentGuardian>> CreateGuardianAsync(
        ProvisionGuardianRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ProvisionResult<StudentGuardian>.Fail("Guardian email is required.");
        }

        if (await _userManager.FindByEmailAsync(request.Email) is not null)
        {
            return ProvisionResult<StudentGuardian>.Fail($"The email '{request.Email}' is already registered.");
        }

        var student = await _db.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.IsActive, cancellationToken);

        if (student is null)
        {
            return ProvisionResult<StudentGuardian>.Fail("The selected student does not exist or is inactive.");
        }

        var generatedPassword = string.IsNullOrWhiteSpace(request.Password)
            ? GenerateTemporaryPassword()
            : null;

        return await ExecuteInTransactionAsync(async () =>
        {
            var userResult = await CreateUserAsync(
                request.Email,
                request.FullName,
                generatedPassword ?? request.Password!,
                Roles.Guardian);

            if (userResult.User is null)
            {
                return ProvisionResult<StudentGuardian>.Fail(userResult.Errors);
            }

            var guardian = new Guardian
            {
                UserId = userResult.User.Id,
                PhoneNumber = request.PhoneNumber,
                CreatedAt = DateTime.UtcNow
            };

            _db.Guardians.Add(guardian);
            await _db.SaveChangesAsync(cancellationToken);

            var link = new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relationship = string.IsNullOrWhiteSpace(request.Relationship) ? "Guardian" : request.Relationship.Trim(),
                IsPrimaryContact = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentGuardians.Add(link);
            await _db.SaveChangesAsync(cancellationToken);

            return ProvisionResult<StudentGuardian>.Ok(link, generatedPassword);
        }, request.Email, cancellationToken);
    }

    public async Task<ProvisionResult<Guardian>> CreateGuardianAccountAsync(
        ProvisionGuardianAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ProvisionResult<Guardian>.Fail("Guardian email is required.");
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return ProvisionResult<Guardian>.Fail($"The email '{email}' is already registered.");
        }

        var generatedPassword = string.IsNullOrWhiteSpace(request.Password)
            ? GenerateTemporaryPassword()
            : null;

        return await ExecuteInTransactionAsync(async () =>
        {
            var userResult = await CreateUserAsync(
                email,
                request.FullName,
                generatedPassword ?? request.Password!,
                Roles.Guardian);

            if (userResult.User is null)
            {
                return ProvisionResult<Guardian>.Fail(userResult.Errors);
            }

            var guardian = new Guardian
            {
                UserId = userResult.User.Id,
                PhoneNumber = request.PhoneNumber?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.Guardians.Add(guardian);
            await _db.SaveChangesAsync(cancellationToken);

            return ProvisionResult<Guardian>.Ok(guardian, generatedPassword);
        }, email, cancellationToken);
    }

    public string GenerateTemporaryPassword()
    {
        // One character from each required class, then padded to 12 and shuffled so the
        // required characters are not always in the same position.
        var characters = new List<char>
        {
            PasswordUpper[RandomNumberGenerator.GetInt32(PasswordUpper.Length)],
            PasswordLower[RandomNumberGenerator.GetInt32(PasswordLower.Length)],
            PasswordDigits[RandomNumberGenerator.GetInt32(PasswordDigits.Length)],
            PasswordSymbols[RandomNumberGenerator.GetInt32(PasswordSymbols.Length)]
        };

        const string pool = PasswordUpper + PasswordLower + PasswordDigits + PasswordSymbols;
        while (characters.Count < 12)
        {
            characters.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
        }

        for (var i = characters.Count - 1; i > 0; i--)
        {
            var swap = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[swap]) = (characters[swap], characters[i]);
        }

        return new string(characters.ToArray());
    }

    /// <summary>
    /// Connection resiliency is enabled, and a retrying execution strategy refuses to run a
    /// manually started transaction unless the whole unit is executed through it.
    /// </summary>
    private async Task<ProvisionResult<T>> ExecuteInTransactionAsync<T>(
        Func<Task<ProvisionResult<T>>> work,
        string accountDescription,
        CancellationToken cancellationToken,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) where T : class
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

            try
            {
                var result = await work();

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result;
                }

                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to provision {AccountDescription}", accountDescription);
                return ProvisionResult<T>.Fail(
                    "Could not save the profile. The generated identifier may have just been taken - please retry.");
            }
        });
    }

    private async Task<(ApplicationUser? User, string[] Errors)> CreateUserAsync(
        string email,
        string fullName,
        string password,
        string role,
        string? photoUrl = null)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName.Trim(),
            PhotoUrl = photoUrl,
            ProfileImageUrl = photoUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return (null, createResult.Errors.Select(e => e.Description).ToArray());
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        return roleResult.Succeeded
            ? (user, [])
            : (null, roleResult.Errors.Select(e => e.Description).ToArray());
    }

    private async Task LogPhotoPersistenceAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (user.PhotoUrl is null)
        {
            return;
        }

        var persistedPhotoUrl = await _db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == user.Id)
            .Select(candidate => candidate.PhotoUrl)
            .SingleAsync(cancellationToken);

        if (!string.Equals(persistedPhotoUrl, user.PhotoUrl, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Profile photo path was not persisted for user {user.Id}.");
        }

        _logger.LogInformation(
            "Verified profile photo persisted for user {UserId}: {PhotoUrl}",
            user.Id,
            persistedPhotoUrl);
    }

    /// <summary>Builds the next identifier of the form PREFIX-yyyy-0001.</summary>
    private async Task<string> GenerateSequenceAsync(string prefix, CancellationToken cancellationToken)
    {
        var stem = $"{prefix}-{DateTime.UtcNow.Year}-";

        if (prefix == "sms")
        {
            var identifiers = await _db.Students
                .Where(s => s.StudentIdNumber.StartsWith(stem))
                .Select(s => s.StudentIdNumber)
                .ToListAsync(cancellationToken);

            var highestNumber = 0;
            foreach (var identifier in identifiers)
            {
                if (int.TryParse(
                    identifier.AsSpan(stem.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsed))
                {
                    highestNumber = Math.Max(highestNumber, parsed);
                }
            }

            return $"{stem}{highestNumber + 1:D4}";
        }

        var last = await _db.Teachers
            .Where(t => t.EmployeeId.StartsWith(stem))
            .OrderByDescending(t => t.EmployeeId)
            .Select(t => t.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);
        var next = last is not null && int.TryParse(last[stem.Length..], out var lastParsed)
            ? lastParsed + 1
            : 1;

        return $"{stem}{next:D4}";
    }

}
