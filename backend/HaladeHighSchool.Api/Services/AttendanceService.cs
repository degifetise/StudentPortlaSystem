using HaladeHighSchool.Api.Data;
using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Services;

public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _db;
    private readonly ITeachingAssignmentService _assignments;

    public AttendanceService(ApplicationDbContext db, ITeachingAssignmentService assignments)
    {
        _db = db;
        _assignments = assignments;
    }

    public async Task<IReadOnlyList<AttendanceResponse>> ListAsync(
        int? studentId,
        int? sectionId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<int>? allowedSectionIds = null)
    {
        var query = _db.Attendance
            .AsNoTracking()
            .Where(a => studentId == null || a.StudentId == studentId)
            .Where(a => sectionId == null || a.Student!.SectionId == sectionId)
            .Where(a => from == null || a.AttendanceDate >= from)
            .Where(a => to == null || a.AttendanceDate <= to);

        if (allowedSectionIds is not null)
        {
            query = query.Where(a => allowedSectionIds.Contains(a.Student!.SectionId));
        }

        return await Project(query)
            .OrderByDescending(a => a.AttendanceDate)
            .ThenBy(a => a.StudentIdNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceSummaryResponse>> SummaryAsync(
        int? studentId,
        int? sectionId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<int>? allowedSectionIds = null)
    {
        var query = _db.Attendance
            .AsNoTracking()
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to)
            .Where(a => studentId == null || a.StudentId == studentId)
            .Where(a => sectionId == null || a.Student!.SectionId == sectionId);

        if (allowedSectionIds is not null)
        {
            query = query.Where(a => allowedSectionIds.Contains(a.Student!.SectionId));
        }

        return await query
            .GroupBy(a => new
            {
                a.StudentId,
                a.Student!.StudentIdNumber,
                StudentName = a.Student.User != null ? a.Student.User.FullName : a.Student.StudentIdNumber,
            })
            .Select(g => new AttendanceSummaryResponse
            {
                StudentId = g.Key.StudentId,
                StudentIdNumber = g.Key.StudentIdNumber,
                StudentName = g.Key.StudentName,
                PresentCount = g.Count(a => a.Status == AttendanceStatuses.Present),
                AbsentCount = g.Count(a => a.Status == AttendanceStatuses.Absent),
                LateCount = g.Count(a => a.Status == AttendanceStatuses.Late),
                ExcusedCount = g.Count(a => a.Status == AttendanceStatuses.Excused),
                TotalCount = g.Count(),
            })
            .OrderBy(s => s.StudentName)
            .ToListAsync(cancellationToken);
    }

    public async Task<AttendanceOperationResult> MarkAsync(
        MarkAttendanceRequest request,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.IsActive, cancellationToken);

        if (student is null)
        {
            return AttendanceOperationResult.Missing($"Student {request.StudentId} was not found.");
        }

        if (!isAdmin && (teacherId is null || !await _assignments.IsAssignedAsync(
                teacherId.Value, request.SubjectId, student.SectionId, cancellationToken)))
        {
            return AttendanceOperationResult.Fail(
                "You are not assigned to the requested subject and section.");
        }

        var attendance = await _db.Attendance
            .FirstOrDefaultAsync(
                a => a.StudentId == request.StudentId && a.AttendanceDate == request.AttendanceDate,
                cancellationToken);

        if (attendance is null)
        {
            attendance = new Attendance
            {
                StudentId = request.StudentId,
                AttendanceDate = request.AttendanceDate,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Attendance.Add(attendance);
        }
        else
        {
            attendance.UpdatedAt = DateTime.UtcNow;
        }

        attendance.Status = request.Status;
        attendance.RecordedByTeacherId = isAdmin ? null : teacherId;
        attendance.Remark = request.Remark?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        return AttendanceOperationResult.Ok(await ListAsync(
            request.StudentId,
            null,
            request.AttendanceDate,
            request.AttendanceDate,
            cancellationToken));
    }

    public async Task<AttendanceOperationResult> BulkMarkAsync(
        BulkMarkAttendanceRequest request,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var duplicateIds = request.Entries
            .GroupBy(e => e.StudentId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            return AttendanceOperationResult.Fail(
                $"Each student may appear once. Repeated: {string.Join(", ", duplicateIds)}.");
        }

        var students = await _db.Students
            .Where(s => s.IsActive && s.SectionId == request.SectionId)
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var missingIds = request.Entries.Select(e => e.StudentId).Except(students.Keys).ToList();
        if (missingIds.Count > 0)
        {
            return AttendanceOperationResult.Fail(
                $"Students not found in section {request.SectionId}: {string.Join(", ", missingIds)}.");
        }

        if (!isAdmin && (teacherId is null || !await _assignments.IsAssignedAsync(
                teacherId.Value, request.SubjectId, request.SectionId, cancellationToken)))
        {
            return AttendanceOperationResult.Fail(
                "You are not assigned to the requested subject and section.");
        }

        var studentIds = request.Entries.Select(e => e.StudentId).ToList();
        var existing = await _db.Attendance
            .Where(a => studentIds.Contains(a.StudentId) && a.AttendanceDate == request.AttendanceDate)
            .ToDictionaryAsync(a => a.StudentId, cancellationToken);

        foreach (var entry in request.Entries)
        {
            if (!existing.TryGetValue(entry.StudentId, out var attendance))
            {
                attendance = new Attendance
                {
                    StudentId = entry.StudentId,
                    AttendanceDate = request.AttendanceDate,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Attendance.Add(attendance);
            }
            else
            {
                attendance.UpdatedAt = DateTime.UtcNow;
            }

            attendance.Status = entry.Status;
            attendance.RecordedByTeacherId = isAdmin ? null : teacherId;
            attendance.Remark = entry.Remark?.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return AttendanceOperationResult.Ok(await ListAsync(
            null,
            request.SectionId,
            request.AttendanceDate,
            request.AttendanceDate,
            cancellationToken));
    }

    private static IQueryable<AttendanceResponse> Project(IQueryable<Attendance> query) => query
        .Select(a => new AttendanceResponse
        {
            Id = a.Id,
            StudentId = a.StudentId,
            StudentIdNumber = a.Student!.StudentIdNumber,
            StudentName = a.Student.User != null ? a.Student.User.FullName : a.Student.StudentIdNumber,
            AttendanceDate = a.AttendanceDate,
            Status = a.Status,
            RecordedByTeacherId = a.RecordedByTeacherId,
            RecordedByTeacherName = a.RecordedByTeacher != null && a.RecordedByTeacher.User != null
                ? a.RecordedByTeacher.User.FullName
                : null,
            Remark = a.Remark,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
        });
}
