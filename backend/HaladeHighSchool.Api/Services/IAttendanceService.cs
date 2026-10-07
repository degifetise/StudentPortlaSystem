using HaladeHighSchool.Api.DTOs;

namespace HaladeHighSchool.Api.Services;

public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceResponse>> ListAsync(
        int? studentId,
        int? sectionId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<int>? allowedSectionIds = null);

    Task<IReadOnlyList<AttendanceSummaryResponse>> SummaryAsync(
        int? studentId,
        int? sectionId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<int>? allowedSectionIds = null);

    Task<AttendanceOperationResult> MarkAsync(
        MarkAttendanceRequest request,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<AttendanceOperationResult> BulkMarkAsync(
        BulkMarkAttendanceRequest request,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}

public record AttendanceOperationResult
{
    public bool Succeeded { get; init; }
    public bool NotFound { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<AttendanceResponse> Items { get; init; } = [];

    public static AttendanceOperationResult Ok(IReadOnlyList<AttendanceResponse> items) =>
        new() { Succeeded = true, Items = items };

    public static AttendanceOperationResult Fail(params string[] errors) =>
        new() { Errors = errors };

    public static AttendanceOperationResult Missing(string error) =>
        new() { NotFound = true, Errors = [error] };
}
