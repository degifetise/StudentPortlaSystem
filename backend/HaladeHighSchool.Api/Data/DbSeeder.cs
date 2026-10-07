using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Data;

/* Seed account values are read from configuration. Keep passwords and signing keys
   in .NET user-secrets or environment variables, never in appsettings files.
   See README_CREDENTIALS.md for local setup instructions. */

/// <summary>
/// Creates the artefacts the Phase 1 T-SQL script cannot: Identity password hashes must be
/// produced by <see cref="IPasswordHasher{TUser}"/>, so accounts are created here on
/// start-up instead. Idempotent - an account that already exists is left untouched.
/// </summary>
public static class DbSeeder
{
    /// <summary>One seeded login, for the Development credential summary.</summary>
    private sealed record SeededAccount(string Role, string Email, string Password, bool WasCreated);

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await SeedRolesAsync(roleManager, logger);

        var accounts = new List<SeededAccount>();

        if (await SeedAdminAsync(userManager, configuration, logger) is { } admin)
        {
            accounts.Add(admin);
        }

        await EnsureAllTeacherRolesAsync(
            services.GetRequiredService<ApplicationDbContext>(),
            userManager,
            logger,
            cancellationToken);

        // Demo teacher and student, so a fresh clone can sign in as all three roles.
        if (environment.IsDevelopment())
        {
            accounts.AddRange(await SeedDemoAccountsAsync(services, configuration, userManager, logger, cancellationToken));
            LogCredentialSummary(accounts, logger);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created missing role {Role}", role);
            }
        }
    }

    private static async Task<SeededAccount?> SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        var fullName = configuration["SeedAdmin:FullName"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedAdmin configuration is missing; no administrator was created.");
            return null;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return new SeededAccount(Roles.Admin, email, password, WasCreated: false);
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Failed to create the seed administrator: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Seeded administrator account {Email}", email);

        return new SeededAccount(Roles.Admin, email, password, WasCreated: true);
    }

    /// <summary>
    /// Development-only teacher and student. Both go through
    /// <see cref="IAccountProvisioningService"/> so they get the same generated employee and
    /// student numbers, transaction handling and validation as an account created by an admin.
    /// </summary>
    private static async Task<List<SeededAccount>> SeedDemoAccountsAsync(
        IServiceProvider services,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var seeded = new List<SeededAccount>();
        var section = configuration.GetSection("SeedDemoAccounts");

        if (!section.Exists() || !section.GetValue("Enabled", false))
        {
            logger.LogInformation("SeedDemoAccounts is absent or disabled; only the administrator was seeded.");
            return seeded;
        }

        var teacherEmail = section["Teacher:Email"];
        // Per-role passwords, falling back to a shared one so either style of config works.
        var teacherPassword = section["Teacher:Password"] ?? section["Password"];
        var studentPassword = section["Student:Password"] ?? section["Password"];

        if (string.IsNullOrWhiteSpace(teacherEmail) ||
            string.IsNullOrWhiteSpace(teacherPassword) ||
            string.IsNullOrWhiteSpace(studentPassword))
        {
            logger.LogWarning("SeedDemoAccounts is incomplete; no demo accounts were created.");
            return seeded;
        }

        var provisioning = services.GetRequiredService<IAccountProvisioningService>();

        // ---- Teacher -------------------------------------------------------
        var existingTeacher = await userManager.FindByEmailAsync(teacherEmail);
        if (existingTeacher is not null)
        {
            await EnsureRoleAsync(userManager, existingTeacher, Roles.Teacher, logger);
            seeded.Add(new SeededAccount(Roles.Teacher, teacherEmail, teacherPassword, WasCreated: false));
        }
        else
        {
            var result = await provisioning.CreateTeacherAsync(new ProvisionTeacherRequest
            {
                Email = teacherEmail,
                Password = teacherPassword,
                FullName = section["Teacher:FullName"] ?? "Demo Teacher",
                Specialization = section["Teacher:Specialization"]
            }, cancellationToken);

            if (result.Succeeded)
            {
                logger.LogInformation("Seeded demo teacher {Email}", teacherEmail);
                seeded.Add(new SeededAccount(Roles.Teacher, teacherEmail, teacherPassword, WasCreated: true));
            }
            else
            {
                logger.LogWarning(
                    "Could not seed the demo teacher: {Errors}",
                    string.Join("; ", result.Errors));
            }
        }

        // ---- Student -------------------------------------------------------
        var db = services.GetRequiredService<ApplicationDbContext>();
        var studentFullName = section["Student:FullName"] ?? "Demo Student";
        var existingStudentUser = await db.Students
            .Where(student => student.User != null && student.User.FullName == studentFullName)
            .Select(student => student.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingStudentUser is not null)
        {
            await EnsureRoleAsync(userManager, existingStudentUser, Roles.Student, logger);
            seeded.Add(new SeededAccount(
                Roles.Student,
                existingStudentUser.Email ?? string.Empty,
                studentPassword,
                WasCreated: false));
            return seeded;
        }

        /* A student needs a class. Both are matched on the values no API can change -
           the lowest grade Level and the alphabetically first section Code - because an
           admin may have renamed 'Grade 9' or 'Section A' by now. */
        var gradeLevelId = await db.GradeLevels
            .Where(g => g.IsActive)
            .OrderBy(g => g.Level)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var sectionId = await db.Sections
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (gradeLevelId is null || sectionId is null)
        {
            logger.LogWarning(
                "No active grade level or section exists, so the demo student was skipped. " +
                "Run database/01_Create_HaladeHighSchoolDb.sql to seed the academic structure.");
            return seeded;
        }

        var studentResult = await provisioning.CreateStudentAsync(new ProvisionStudentRequest
        {
            Password = studentPassword,
            FullName = studentFullName,
            GradeLevelId = gradeLevelId.Value,
            SectionId = sectionId.Value
        }, cancellationToken);

        if (studentResult.Succeeded && studentResult.Entity is not null)
        {
            var studentUser = await userManager.FindByIdAsync(studentResult.Entity.UserId!);
            if (studentUser is null)
            {
                logger.LogError("The demo student was created without a matching login.");
                return seeded;
            }

            var studentEmail = studentUser.Email ?? string.Empty;
            logger.LogInformation("Seeded demo student {Email}", studentEmail);
            seeded.Add(new SeededAccount(Roles.Student, studentEmail, studentPassword, WasCreated: true));
        }
        else
        {
            logger.LogWarning(
                "Could not seed the demo student: {Errors}",
                string.Join("; ", studentResult.Errors));
        }

        return seeded;
    }

    private static async Task EnsureRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string role,
        ILogger logger)
    {
        if (await userManager.IsInRoleAsync(user, role))
        {
            return;
        }

        var result = await userManager.AddToRoleAsync(user, role);
        if (result.Succeeded)
        {
            logger.LogInformation("Repaired {Role} role membership for {Email}", role, user.Email);
        }
        else
        {
            logger.LogError(
                "Failed to assign {Role} role to {Email}: {Errors}",
                role,
                user.Email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task EnsureAllTeacherRolesAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var teacherUserIds = await db.Teachers
            .AsNoTracking()
            .Where(t => t.UserId != null)
            .Select(t => t.UserId!)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in teacherUserIds)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                await EnsureRoleAsync(userManager, user, Roles.Teacher, logger);
            }
        }
    }

    /// <summary>Logs the number of processed demo accounts without disclosing credentials.</summary>
    private static void LogCredentialSummary(List<SeededAccount> accounts, ILogger logger)
    {
        if (accounts.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Development seeding processed {AccountCount} demo accounts; credentials were not logged.",
            accounts.Count);
    }
}
