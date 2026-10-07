using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using HaladeHighSchool.Api.Configuration;
using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HaladeHighSchool.Api.Controllers;

[ApiController]
[Route("api/smart-id")]
[Produces("application/json")]
[Authorize]
public sealed class SmartIDController : PortalControllerBase
{
    private const int MaximumBatchSize = 100;
    private const string SmartIdIssuer = "HaladeHighSchool.SmartID";
    private const string SmartIdAudience = "smart-id-verification";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SmartIDCardService _cardService;
    private readonly SmartIDTokenKeyProtector _tokenKeyProtector;
    private readonly IProfilePhotoStorage _photoStorage;
    private readonly ILogger<SmartIDController> _logger;

    public SmartIDController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        SmartIDCardService cardService,
        SmartIDTokenKeyProtector tokenKeyProtector,
        IProfilePhotoStorage photoStorage,
        ILogger<SmartIDController> logger)
    {
        _db = db;
        _userManager = userManager;
        _cardService = cardService;
        _tokenKeyProtector = tokenKeyProtector;
        _photoStorage = photoStorage;
        _logger = logger;
    }

    [HttpGet("card-details/{userId}")]
    public async Task<ActionResult<SmartIDCardDetails>> GetCardDetails(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!CanReadUserCardAsync(userId))
        {
            return Forbid();
        }

        var details = await FindCardDetailsAsync(userId, cancellationToken);
        if (details is not null && (details.CardStatus != "ACTIVE" || details.ExpirationDate <= DateTime.UtcNow))
        {
            details = null;
        }
        return details is null ? NotFoundProblem("No Smart ID card is available for this user.") : Ok(details);
    }

    [HttpGet("payment-status/{userId}")]
    [ProducesResponseType<SmartIDPaymentStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SmartIDPaymentStatusResponse>> GetPaymentStatus(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!CanReadUserCardAsync(userId))
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var cardGenerated = await _db.SmartCards.AsNoTracking()
            .AnyAsync(card => card.UserId == userId && card.Status == "ACTIVE" && card.ExpirationDate > now, cancellationToken);

        return Ok(new SmartIDPaymentStatusResponse(
            false,
            cardGenerated,
            cardGenerated,
            0m,
            0m,
            0m));
    }

    [HttpGet("admin/users/{identifier}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SmartIDAdminUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SmartIDAdminUserResponse>> LookupAdminUser(
        string identifier,
        CancellationToken cancellationToken)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("No student or staff account matches that ID.");
        }

        return Ok(await ToAdminUserResponseAsync(user, cancellationToken));
    }

    [HttpGet("admin/cards")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<IReadOnlyList<SmartIDAdminUserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SmartIDAdminUserResponse>>> GetAdminCards(
        [FromQuery] string? search,
        [FromQuery] string? role,
        CancellationToken cancellationToken)
    {
        var normalizedRole = string.IsNullOrWhiteSpace(role) ? null : role.Trim().ToLowerInvariant();
        if (normalizedRole is not null && normalizedRole is not ("student" or "teacher" or "staff"))
        {
            return BadRequestProblem("Invalid role", "Choose student, teacher or staff.");
        }

        var candidates = new List<AdminLookupCandidate>();
        var searchTerm = search?.Trim();
        if (normalizedRole is null or "student")
        {
            var students = _db.Students.AsNoTracking()
                .Include(student => student.User)
                .Include(student => student.GradeLevel)
                .Include(student => student.Section)
                .Where(student => student.IsActive && student.UserId != null);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                students = students.Where(student =>
                    student.StudentIdNumber.Contains(searchTerm)
                    || student.User!.FullName.Contains(searchTerm)
                    || student.User.Email!.Contains(searchTerm));
            }

            candidates.AddRange(await students.OrderBy(student => student.StudentIdNumber)
                .Take(MaximumBatchSize)
                .Select(student => new AdminLookupCandidate(
                    student.User!,
                    "Student",
                    student.StudentIdNumber,
                    student.GradeLevel!.Name,
                    student.Section!.Name))
                .ToListAsync(cancellationToken));
        }

        if (normalizedRole is null or "teacher")
        {
            var teachers = _db.Teachers.AsNoTracking()
                .Include(teacher => teacher.User)
                .Where(teacher => teacher.IsActive && teacher.UserId != null);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                teachers = teachers.Where(teacher =>
                    teacher.EmployeeId.Contains(searchTerm)
                    || teacher.User!.FullName.Contains(searchTerm)
                    || teacher.User.Email!.Contains(searchTerm));
            }

            candidates.AddRange(await teachers.OrderBy(teacher => teacher.EmployeeId)
                .Take(MaximumBatchSize)
                .Select(teacher => new AdminLookupCandidate(
                    teacher.User!,
                    "Teacher",
                    teacher.EmployeeId,
                    teacher.Specialization,
                    null))
                .ToListAsync(cancellationToken));
        }

        if (normalizedRole is null or "staff")
        {
            var staffUsers = await _userManager.GetUsersInRoleAsync(Roles.Staff);
            if (normalizedRole is null)
            {
                staffUsers = staffUsers
                    .Concat(await _userManager.GetUsersInRoleAsync(Roles.Admin))
                    .DistinctBy(user => user.Id)
                    .ToList();
            }

            candidates.AddRange(staffUsers
                .Where(user => string.IsNullOrWhiteSpace(searchTerm)
                    || user.Id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || (user.Email?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
                    || user.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Take(MaximumBatchSize)
                .Select(user => new AdminLookupCandidate(
                    user,
                    "Staff",
                    user.Email ?? user.Id,
                    null,
                    null)));
        }

        var uniqueCandidates = candidates
            .DistinctBy(candidate => candidate.User.Id)
            .Take(MaximumBatchSize)
            .ToList();
        var ids = uniqueCandidates.Select(candidate => candidate.User.Id).ToList();
        var cardRows = await CardDetailsQuery()
            .Where(card => ids.Contains(card.UserId))
            .ToListAsync(cancellationToken);
        var cardsByUserId = cardRows
            .Where(card => card.User is not null)
            .ToDictionary(card => card.UserId, card => ToCardDetails(card, card.User!));

        return Ok(uniqueCandidates.Select(candidate =>
        {
            cardsByUserId.TryGetValue(candidate.User.Id, out var card);
            return new SmartIDAdminUserResponse(
                candidate.User.Id,
                candidate.User.FullName,
                candidate.Role,
                candidate.Identifier,
                candidate.DepartmentOrGrade,
                candidate.Section,
                candidate.User.PhotoUrl ?? candidate.User.ProfileImageUrl,
                card is not null,
                card);
        }).ToList());
    }

    [HttpPost("admin/users/{userId}/generate")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SmartIDCardDetails>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SmartIDCardDetails>> GenerateCardForUser(
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFoundProblem("The requested account was not found.");
        }

        if (await _db.SmartCards.AnyAsync(card => card.UserId == userId, cancellationToken))
        {
            return ConflictProblem("Smart ID already generated", "This account already has a Smart ID.");
        }

        var cardType = await ResolveCardTypeAsync(user);
        if (cardType is null)
        {
            return BadRequestProblem("Unsupported account", "Only student, teacher, and staff accounts can receive a Smart ID.");
        }

        await _cardService.AddCardAsync(user, cardType, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var details = await FindCardDetailsAsync(userId, cancellationToken);
        if (details is null)
        {
            throw new InvalidOperationException("The Smart ID was generated but could not be loaded.");
        }

        return CreatedAtAction(nameof(GetCardDetails), new { userId }, details);
    }

    [HttpPost("batch-cards")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<IReadOnlyList<SmartIDCardDetails>>> GetBatchCards(
        [FromBody] BatchSmartIDRequest request,
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] string? role,
        [FromQuery] string? department,
        CancellationToken cancellationToken)
    {
        var selectedRole = string.IsNullOrWhiteSpace(role) ? "student" : role.Trim().ToLowerInvariant();
        if (selectedRole is not ("student" or "teacher" or "staff"))
        {
            return BadRequestProblem("Invalid role", "Choose student, teacher or staff.");
        }

        var userIds = request.UserIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (userIds is { Count: > MaximumBatchSize })
        {
            return BadRequestProblem("Batch too large", $"A print batch is limited to {MaximumBatchSize} cards.");
        }

        if (userIds is null or { Count: 0 })
        {
            if (selectedRole == "staff")
            {
                var staffUsers = (await _userManager.GetUsersInRoleAsync(Roles.Staff))
                    .Concat(await _userManager.GetUsersInRoleAsync(Roles.Admin))
                    .DistinctBy(user => user.Id);
                userIds = staffUsers
                    .Select(user => user.Id)
                    .Take(MaximumBatchSize + 1)
                    .ToList();
            }
            else if (selectedRole == "teacher")
            {
                if (string.IsNullOrWhiteSpace(department))
                {
                    return BadRequestProblem("Filter required", "Provide a department filter or explicit userIds.");
                }

                var departmentFilter = department.Trim();
                userIds = await _db.Teachers.AsNoTracking()
                    .Where(teacher => teacher.IsActive && teacher.UserId != null
                        && teacher.Specialization != null
                        && teacher.Specialization.Contains(departmentFilter))
                    .OrderBy(teacher => teacher.EmployeeId)
                    .Select(teacher => teacher.UserId!)
                    .Take(MaximumBatchSize + 1)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(grade) && string.IsNullOrWhiteSpace(section))
                {
                    return BadRequestProblem("Filter required", "Provide userIds or a grade/section filter.");
                }

                var students = _db.Students.AsNoTracking().Where(student => student.IsActive && student.UserId != null);
                if (!string.IsNullOrWhiteSpace(grade))
                {
                    var gradeFilter = grade.Trim();
                    students = int.TryParse(gradeFilter, out var level)
                        ? students.Where(student => student.GradeLevel!.Level == level
                            || student.GradeLevel!.Name == gradeFilter)
                        : students.Where(student => student.GradeLevel!.Name == gradeFilter);
                }

                if (!string.IsNullOrWhiteSpace(section))
                {
                    var sectionFilter = section.Trim();
                    students = students.Where(student => student.Section!.Name == sectionFilter
                        || student.Section.Code == sectionFilter);
                }

                userIds = await students
                    .OrderBy(student => student.GradeLevel!.Level)
                    .ThenBy(student => student.Section!.Code)
                    .ThenBy(student => student.StudentIdNumber)
                    .Select(student => student.UserId!)
                    .Take(MaximumBatchSize + 1)
                    .ToListAsync(cancellationToken);
            }
            if (userIds.Count > MaximumBatchSize)
            {
                return BadRequestProblem("Batch too large", $"A print batch is limited to {MaximumBatchSize} cards.");
            }
        }

        var cardRows = await CardDetailsQuery()
            .Where(card => userIds.Contains(card.UserId))
            .ToListAsync(cancellationToken);
        var cardsByUserId = cardRows
            .Where(card => card.User is not null)
            .ToDictionary(card => card.UserId, card => ToCardDetails(card, card.User!));
        return Ok(userIds.Where(cardsByUserId.ContainsKey).Select(userId => cardsByUserId[userId]).ToList());
    }

    [HttpPost("generate-cards")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<IReadOnlyList<SmartIDCardDetails>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SmartIDCardDetails>>> GenerateCards(
        [FromBody] BatchSmartIDRequest request,
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] string? role,
        [FromQuery] string? department,
        CancellationToken cancellationToken)
    {
        var selectedRole = string.IsNullOrWhiteSpace(role) ? "student" : role.Trim().ToLowerInvariant();
        if (selectedRole is not ("student" or "teacher" or "staff"))
        {
            return BadRequestProblem("Invalid role", "Choose student, teacher or staff.");
        }

        var requestedIds = request.UserIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        if (requestedIds is { Count: > MaximumBatchSize })
        {
            return BadRequestProblem("Batch too large", $"A generation batch is limited to {MaximumBatchSize} cards.");
        }

        List<string> eligibleIds;
        if (selectedRole == "student")
        {
            if ((requestedIds is null or { Count: 0 }) && string.IsNullOrWhiteSpace(grade) && string.IsNullOrWhiteSpace(section))
            {
                return BadRequestProblem("Filter required", "Provide userIds or a grade/section filter.");
            }

            var students = _db.Students.AsNoTracking()
                .Where(student => student.IsActive && student.UserId != null);
            if (requestedIds is { Count: > 0 })
            {
                students = students.Where(student => requestedIds.Contains(student.UserId!));
            }
            if (!string.IsNullOrWhiteSpace(grade))
            {
                var gradeFilter = grade.Trim();
                students = int.TryParse(gradeFilter, out var level)
                    ? students.Where(student => student.GradeLevel!.Level == level || student.GradeLevel!.Name == gradeFilter)
                    : students.Where(student => student.GradeLevel!.Name == gradeFilter);
            }
            if (!string.IsNullOrWhiteSpace(section))
            {
                var sectionFilter = section.Trim();
                students = students.Where(student => student.Section!.Name == sectionFilter || student.Section.Code == sectionFilter);
            }

            eligibleIds = await students.Select(student => student.UserId!)
                .Take(MaximumBatchSize + 1)
                .ToListAsync(cancellationToken);
        }
        else if (selectedRole == "teacher")
        {
            if ((requestedIds is null or { Count: 0 }) && string.IsNullOrWhiteSpace(department))
            {
                return BadRequestProblem("Filter required", "Provide userIds, or a department filter for teachers.");
            }

            var teachers = _db.Teachers.AsNoTracking()
                .Where(teacher => teacher.IsActive && teacher.UserId != null);
            if (requestedIds is { Count: > 0 })
            {
                teachers = teachers.Where(teacher => requestedIds.Contains(teacher.UserId!));
            }
            if (!string.IsNullOrWhiteSpace(department))
            {
                var departmentFilter = department.Trim();
                teachers = teachers.Where(teacher => teacher.Specialization != null
                    && teacher.Specialization.Contains(departmentFilter));
            }

            eligibleIds = await teachers.Select(teacher => teacher.UserId!)
                .Take(MaximumBatchSize + 1)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var staffUsers = (await _userManager.GetUsersInRoleAsync(Roles.Staff))
                .Concat(await _userManager.GetUsersInRoleAsync(Roles.Admin))
                .DistinctBy(user => user.Id);
            eligibleIds = staffUsers
                .Where(user => requestedIds is null or { Count: 0 } || requestedIds.Contains(user.Id))
                .Select(user => user.Id)
                .Take(MaximumBatchSize + 1)
                .ToList();
        }

        if (eligibleIds.Count > MaximumBatchSize)
        {
            return BadRequestProblem("Batch too large", $"A generation batch is limited to {MaximumBatchSize} cards.");
        }

        var alreadyGeneratedIds = await _db.SmartCards.AsNoTracking()
            .Where(card => eligibleIds.Contains(card.UserId))
            .Select(card => card.UserId)
            .ToHashSetAsync(cancellationToken);
        var cardType = selectedRole switch
        {
            "student" => "STUDENT",
            "teacher" => "TEACHER",
            _ => "STAFF"
        };

        foreach (var userId in eligibleIds.Where(userId => !alreadyGeneratedIds.Contains(userId)))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                await _cardService.AddCardAsync(user, cardType, cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        var cards = await CardDetailsQuery()
            .Where(card => eligibleIds.Contains(card.UserId))
            .ToListAsync(cancellationToken);
        var cardsByUserId = cards
            .Where(card => card.User is not null)
            .ToDictionary(card => card.UserId, card => ToCardDetails(card, card.User!));
        return Ok(eligibleIds.Where(cardsByUserId.ContainsKey).Select(userId => cardsByUserId[userId]).ToList());
    }

    [HttpPut("cards/{userId}/status")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdateCardStatus(
        string userId,
        UpdateSmartIDCardStatusRequest request,
        CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim().ToUpperInvariant() ?? string.Empty;
        if (status is not ("ACTIVE" or "SUSPENDED" or "LOST" or "EXPIRED"))
        {
            return BadRequestProblem("Invalid card status", "Status must be ACTIVE, SUSPENDED, LOST or EXPIRED.");
        }

        var card = await _db.SmartCards
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (card is null)
        {
            return NotFoundProblem("No Smart ID card is available for this user.");
        }

        card.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { card.UserId, card.Status });
    }

    [HttpPut("admin/cards/{userId}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SmartIDCardDetails>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SmartIDCardDetails>> UpdateAdminCard(
        string userId,
        UpdateSmartIDCardRequest request,
        CancellationToken cancellationToken)
    {
        var card = await _db.SmartCards.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (card is null)
        {
            return NotFoundProblem("No Smart ID card is available for this user.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFoundProblem("The cardholder account was not found.");
        }

        if (request.FullName is not null)
        {
            var fullName = request.FullName.Trim();
            if (fullName.Length is 0 or > 150)
            {
                return BadRequestProblem("Invalid cardholder name", "Full name must contain between 1 and 150 characters.");
            }

            user.FullName = fullName;
            var identityResult = await _userManager.UpdateAsync(user);
            if (!identityResult.Succeeded)
            {
                return BadRequestProblem(
                    "Cardholder update failed",
                    string.Join("; ", identityResult.Errors.Select(error => error.Description)));
            }
        }

        if (request.Status is not null)
        {
            var status = request.Status.Trim().ToUpperInvariant();
            if (status is not ("ACTIVE" or "SUSPENDED" or "LOST" or "EXPIRED"))
            {
                return BadRequestProblem("Invalid card status", "Status must be ACTIVE, SUSPENDED, LOST or EXPIRED.");
            }

            card.Status = status;
        }

        if (request.RotateQrToken)
        {
            var tokenKey = RandomNumberGenerator.GetBytes(32);
            card.QrTokenHash = _tokenKeyProtector.Protect(tokenKey);
            CryptographicOperations.ZeroMemory(tokenKey);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var details = await FindCardDetailsAsync(userId, cancellationToken);
        return details is null ? NotFoundProblem("The updated card could not be loaded.") : Ok(details);
    }

    [HttpDelete("admin/cards/{userId}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAdminCard(string userId, CancellationToken cancellationToken)
    {
        var card = await _db.SmartCards.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (card is null)
        {
            return NotFoundProblem("No Smart ID card is available for this user.");
        }

        _db.SmartCards.Remove(card);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Admin {AdminUserId} revoked Smart ID for user {UserId}", User.GetUserId(), userId);
        return NoContent();
    }

    [HttpGet("generate-qr-token/{userId}")]
    public async Task<ActionResult<GenerateSmartIDTokenResponse>> GenerateQrToken(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!CanReadUserCardAsync(userId))
        {
            return Forbid();
        }

        var card = await _db.SmartCards.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (card is null)
        {
            return NotFoundProblem("No Smart ID card is available for this user.");
        }

        if (card.Status != "ACTIVE" || card.ExpirationDate <= DateTime.UtcNow)
        {
            return ConflictProblem("Card unavailable", "A QR token can only be created for an active, unexpired card.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.Add(TokenLifetime);
        var key = new SymmetricSecurityKey(_tokenKeyProtector.Unprotect(card.QrTokenHash));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: SmartIdIssuer,
            audience: SmartIdAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, card.UserId),
                new Claim("card_uid", card.CardUID),
                new Claim("purpose", "smart-id"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return Ok(new GenerateSmartIDTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt));
    }

    [HttpPost("verify-scan")]
    [Authorize(Roles = Roles.AdminTeacherOrStaff)]
    public async Task<ActionResult<VerifySmartIDScanResponse>> VerifyScan(
        VerifySmartIDScanRequest request,
        CancellationToken cancellationToken)
    {
        var input = request.Code?.Trim() ?? string.Empty;
        if (input.Length is 0 or > 4096)
        {
            return BadRequestProblem("Invalid scan", "Provide a valid card UID or Smart ID QR token.");
        }

        if (request.ScanLocation?.Length > 200)
        {
            return BadRequestProblem("Invalid scan location", "Scan location must be 200 characters or fewer.");
        }

        var scannerUserId = User.GetUserId();
        if (scannerUserId is null)
        {
            return Unauthorized();
        }
        string? cardUid = input;
        JwtSecurityToken? presentedToken = null;
        var isDynamicToken = input.Count(character => character == '.') == 2;
        if (isDynamicToken)
        {
            try
            {
                presentedToken = new JwtSecurityTokenHandler().ReadJwtToken(input);
                cardUid = presentedToken.Claims.FirstOrDefault(claim => claim.Type == "card_uid")?.Value;
            }
            catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
            {
                cardUid = null;
            }
        }

        var card = cardUid is null
            ? null
            : await _db.SmartCards.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CardUID == cardUid, cancellationToken);

        var status = card is null ? "NOT_FOUND" : "INVALID";
        var details = card is null ? null : await FindCardDetailsAsync(card.UserId, cancellationToken);
        var tokenExpired = false;

        if (card is not null && !isDynamicToken)
        {
            status = ResolveCardStatus(card);
        }
        else if (card is not null && presentedToken is not null)
        {
            try
            {
                var signingKey = new SymmetricSecurityKey(_tokenKeyProtector.Unprotect(card.QrTokenHash));
                var principal = new JwtSecurityTokenHandler().ValidateToken(
                    input,
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = SmartIdIssuer,
                        ValidateAudience = true,
                        ValidAudience = SmartIdAudience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = signingKey,
                        ValidateLifetime = false,
                        ClockSkew = TimeSpan.Zero
                    },
                    out _);

                var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var purpose = principal.FindFirstValue("purpose");
                if (subject != card.UserId || purpose != "smart-id")
                {
                    status = "INVALID";
                }
                else if (presentedToken.ValidTo <= DateTime.UtcNow)
                {
                    tokenExpired = true;
                    status = "EXPIRED";
                }
                else
                {
                    status = ResolveCardStatus(card);
                }
            }
            catch (SecurityTokenException)
            {
                status = "INVALID";
            }
            catch (FormatException)
            {
                status = "INVALID";
            }
        }

        if (card is not null && !tokenExpired && status == "VALID")
        {
            status = ResolveCardStatus(card);
        }

        _db.SmartIDScanLogs.Add(new SmartIDScanLog
        {
            ScannerUserId = scannerUserId,
            ScannedUserId = status is "VALID" or "EXPIRED" or "SUSPENDED" or "LOST" ? card?.UserId : null,
            ScanLocation = string.IsNullOrWhiteSpace(request.ScanLocation) ? null : request.ScanLocation.Trim(),
            ScanTimestamp = DateTime.UtcNow,
            ScanStatus = status
        });
        await _db.SaveChangesAsync(cancellationToken);

        if (status is "INVALID" or "NOT_FOUND")
        {
            details = null;
        }

        _logger.LogInformation(
            "Smart ID scan {Status} by scanner {ScannerUserId} at {ScanLocation}",
            status,
            scannerUserId,
            request.ScanLocation);

        return Ok(new VerifySmartIDScanResponse
        {
            Status = status,
            IsValid = status == "VALID",
            Message = status switch
            {
                "VALID" => "Identity verified.",
                "EXPIRED" => "This card or QR token has expired.",
                "SUSPENDED" => "This card is suspended.",
                "LOST" => "This card has been reported lost.",
                "NOT_FOUND" => "No card matches the scanned code.",
                _ => "The QR token is invalid."
            },
            Card = details
        });
    }

    [HttpPost("~/api/users/{userId}/upload-photo")]
    [Authorize(Roles = Roles.Admin)]
    [RequestSizeLimit((2 * 1024 * 1024) + (64 * 1024))]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto(
        string userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFoundProblem("The requested user was not found.");
        }

        string photoUrl;
        try
        {
            photoUrl = await _photoStorage.SaveAsync(file, cancellationToken);
        }
        catch (InvalidProfilePhotoException ex)
        {
            return BadRequestProblem("Invalid photo", ex.Message);
        }

        var previousPhotoUrl = user.PhotoUrl ?? user.ProfileImageUrl;
        user.PhotoUrl = photoUrl;
        user.ProfileImageUrl = photoUrl;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            await _photoStorage.DeleteAsync(photoUrl);
            _logger.LogError("Failed to update photo URL for user {UserId}: {Errors}", userId,
                string.Join("; ", update.Errors.Select(error => error.Code)));
            return Problem("The photo was uploaded but the user profile could not be updated.");
        }

        var persistedPhotoUrl = await _db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => candidate.PhotoUrl)
            .SingleAsync(cancellationToken);
        if (!string.Equals(persistedPhotoUrl, photoUrl, StringComparison.Ordinal))
        {
            await _photoStorage.DeleteAsync(photoUrl);
            throw new InvalidOperationException($"Profile photo path was not persisted for user {userId}.");
        }

        _logger.LogInformation(
            "Verified profile photo persisted for user {UserId}: {PhotoUrl}",
            userId,
            persistedPhotoUrl);
        await _photoStorage.DeleteAsync(previousPhotoUrl);
        return Ok(new { photoUrl });
    }

    [HttpPost("~/api/smartcard/upload-signature/{userId}")]
    [Authorize(Roles = Roles.Admin)]
    [RequestSizeLimit((2 * 1024 * 1024) + (64 * 1024))]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadSignature(
        string userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFoundProblem("The requested user was not found.");
        }

        string signatureUrl;
        try
        {
            signatureUrl = await _photoStorage.SaveSignatureAsync(file, cancellationToken);
        }
        catch (InvalidProfilePhotoException ex)
        {
            return BadRequestProblem("Invalid signature image", ex.Message);
        }

        var previousSignatureUrl = user.DigitalSignatureUrl;
        user.DigitalSignatureUrl = signatureUrl;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            await _photoStorage.DeleteAsync(signatureUrl);
            _logger.LogError(
                "Failed to update signature URL for user {UserId}: {Errors}",
                userId,
                string.Join("; ", update.Errors.Select(error => error.Code)));
            return Problem("The signature was uploaded but the user profile could not be updated.");
        }

        var persistedSignatureUrl = await _db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => candidate.DigitalSignatureUrl)
            .SingleAsync(cancellationToken);
        if (!string.Equals(persistedSignatureUrl, signatureUrl, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Signature path was not persisted for user {userId}.");
        }

        _logger.LogInformation(
            "Verified signature image persisted for user {UserId}: {SignatureUrl}",
            userId,
            persistedSignatureUrl);
        await _photoStorage.DeleteAsync(previousSignatureUrl);
        return Ok(new { digitalSignatureUrl = signatureUrl });
    }

    private bool CanReadUserCardAsync(string userId)
    {
        var callerId = User.GetUserId();
        return callerId == userId || User.IsInRole(Roles.Admin);
    }

    private async Task<ApplicationUser?> FindUserByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        var normalizedIdentifier = identifier.Trim();
        if (normalizedIdentifier.Length == 0)
        {
            return null;
        }

        var studentUser = await _db.Students.AsNoTracking()
            .Where(student => student.StudentIdNumber == normalizedIdentifier)
            .Select(student => student.User)
            .FirstOrDefaultAsync(cancellationToken);
        if (studentUser is not null)
        {
            return studentUser;
        }

        var teacherUser = await _db.Teachers.AsNoTracking()
            .Where(teacher => teacher.EmployeeId == normalizedIdentifier)
            .Select(teacher => teacher.User)
            .FirstOrDefaultAsync(cancellationToken);
        if (teacherUser is not null)
        {
            return teacherUser;
        }

        var staffUsers = (await _userManager.GetUsersInRoleAsync(Roles.Staff))
            .Concat(await _userManager.GetUsersInRoleAsync(Roles.Admin));
        return staffUsers.FirstOrDefault(user =>
            string.Equals(user.Id, normalizedIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Email, normalizedIdentifier, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<SmartIDAdminUserResponse> ToAdminUserResponseAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var student = await _db.Students.AsNoTracking()
            .Include(item => item.GradeLevel)
            .Include(item => item.Section)
            .FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        var teacher = student is null
            ? await _db.Teachers.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken)
            : null;
        var card = await FindCardDetailsAsync(user.Id, cancellationToken);

        var (role, identifier, departmentOrGrade, section) = student is not null
            ? ("Student", student.StudentIdNumber, student.GradeLevel?.Name, student.Section?.Name)
            : teacher is not null
                ? ("Teacher", teacher.EmployeeId, teacher.Specialization, (string?)null)
                : ("Staff", user.Email ?? user.Id, (string?)null, (string?)null);

        return new SmartIDAdminUserResponse(
            user.Id,
            user.FullName,
            role,
            identifier,
            departmentOrGrade,
            section,
            user.PhotoUrl ?? user.ProfileImageUrl,
            card is not null,
            card);
    }

    private async Task<string?> ResolveCardTypeAsync(ApplicationUser user)
    {
        if (await _userManager.IsInRoleAsync(user, Roles.Student))
        {
            return "STUDENT";
        }

        if (await _userManager.IsInRoleAsync(user, Roles.Teacher))
        {
            return "TEACHER";
        }

        if (await _userManager.IsInRoleAsync(user, Roles.Staff)
            || await _userManager.IsInRoleAsync(user, Roles.Admin))
        {
            return "STAFF";
        }

        return null;
    }

    private async Task<SmartIDCardDetails?> FindCardDetailsAsync(string userId, CancellationToken cancellationToken)
    {
        var card = await CardDetailsQuery()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return card?.User is null ? null : ToCardDetails(card, card.User);
    }

    private IQueryable<SmartCard> CardDetailsQuery() =>
        _db.SmartCards.AsNoTracking()
            .Include(card => card.User)!.ThenInclude(user => user!.Student)!.ThenInclude(student => student!.GradeLevel)
            .Include(card => card.User)!.ThenInclude(user => user!.Student)!.ThenInclude(student => student!.Section)
            .Include(card => card.User)!.ThenInclude(user => user!.Teacher);

    private static SmartIDCardDetails ToCardDetails(SmartCard card, ApplicationUser user)
    {
        var identifier = user.Student?.StudentIdNumber
            ?? user.Teacher?.EmployeeId
            ?? card.CardUID;
        var currentYear = DateTime.UtcNow.Month >= 9 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;

        return new SmartIDCardDetails
        {
            UserId = user.Id,
            FullName = user.FullName,
            Role = card.CardType switch
            {
                "STUDENT" => "Student",
                "TEACHER" => "Teacher",
                _ => "Staff"
            },
            Identifier = identifier,
            PhotoUrl = user.PhotoUrl ?? user.ProfileImageUrl,
            GradeLevel = user.Student?.GradeLevel?.Name ?? user.Teacher?.Specialization,
            Section = user.Student?.Section?.Name,
            AcademicYear = $"{currentYear}-{currentYear + 1}",
            DateOfBirth = user.Student?.DateOfBirth,
            EmergencyContact = user.Student?.EmergencyContact ?? user.Student?.GuardianPhone ?? user.PhoneNumber,
            BloodGroup = user.Student?.BloodGroup,
            DigitalSignatureUrl = user.DigitalSignatureUrl,
            CardUID = card.CardUID,
            CardStatus = card.Status,
            IssuedDate = card.IssuedDate,
            ExpirationDate = card.ExpirationDate
        };
    }

    private static string ResolveCardStatus(SmartCard card)
    {
        if (card.ExpirationDate <= DateTime.UtcNow || card.Status == "EXPIRED")
        {
            return "EXPIRED";
        }

        return card.Status switch
        {
            "SUSPENDED" => "SUSPENDED",
            "LOST" => "LOST",
            "ACTIVE" => "VALID",
            _ => "INVALID"
        };
    }

    private sealed record AdminLookupCandidate(
        ApplicationUser User,
        string Role,
        string Identifier,
        string? DepartmentOrGrade,
        string? Section);

}
