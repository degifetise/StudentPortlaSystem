namespace HaladeHighSchool.Api.DTOs;

/// <summary>Notification data visible to its recipient.</summary>
public sealed record NotificationResponse(
    long Id,
    string Title,
    string Message,
    string Type,
    string TargetUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
