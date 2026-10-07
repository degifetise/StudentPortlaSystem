using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : PortalControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(ApplicationDbContext db, ILogger<NotificationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var unreadCount = await _db.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && notification.ReadAt == null, cancellationToken);

        return Ok(new { unreadCount });
    }

    [HttpGet]
    [EndpointSummary("Get recent notifications for the authenticated user")]
    [ProducesResponseType<IReadOnlyList<NotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetNotifications(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.TargetUrl,
                new DateTimeOffset(DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc)),
                notification.ReadAt == null
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind(notification.ReadAt.Value, DateTimeKind.Utc))))
            .ToListAsync(cancellationToken);

        return Ok(notifications);
    }

    [HttpPut("{id:long}/read")]
    [EndpointSummary("Mark one notification as read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (notification is null) return NotFound();

        notification.ReadAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("read-all")]
    [EndpointSummary("Mark all notifications for the authenticated user as read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        await _db.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(
                notification => notification.ReadAt,
                DateTime.UtcNow), cancellationToken);

        return NoContent();
    }
}