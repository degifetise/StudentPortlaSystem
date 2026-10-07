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
[Route("api/staff")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public sealed class StaffController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAccountProvisioningService _provisioning;
    private readonly IProfilePhotoStorage _photoStorage;
    private readonly ILogger<StaffController> _logger;

    public StaffController(
        ApplicationDbContext db,
        IAccountProvisioningService provisioning,
        IProfilePhotoStorage photoStorage,
        ILogger<StaffController> logger)
    {
        _db = db;
        _provisioning = provisioning;
        _photoStorage = photoStorage;
        _logger = logger;
    }

    [HttpPost("with-photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit((2 * 1024 * 1024) + (64 * 1024))]
    [ProducesResponseType<CreateStaffResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateStaffResponse>> CreateStaff(
        [FromForm] CreateStaffRequest request,
        CancellationToken cancellationToken)
    {
        string? photoUrl = null;
        try
        {
            if (request.Photo is not null)
            {
                photoUrl = await _photoStorage.SaveAsync(request.Photo, cancellationToken);
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

        ProvisionResult<ApplicationUser> result;
        try
        {
            result = await _provisioning.CreateStaffAsync(new ProvisionStaffRequest
            {
                Email = request.Email,
                Password = request.Password,
                FullName = request.FullName,
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
                Title = "Staff account not created",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["staff"] = result.Errors.ToArray() }
            });
        }

        _logger.LogInformation(
            "Admin {Admin} provisioned staff account {UserId}",
            User.GetUserId(),
            result.Entity.Id);

        return StatusCode(StatusCodes.Status201Created, new CreateStaffResponse
        {
            UserId = result.Entity.Id,
            FullName = result.Entity.FullName,
            Email = result.Entity.Email ?? request.Email,
            PhotoUrl = result.Entity.PhotoUrl,
            CardUID = null,
            TemporaryPassword = result.TemporaryPassword,
        });
    }
}
