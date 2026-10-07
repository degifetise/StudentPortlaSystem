using HaladeHighSchool.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HaladeHighSchool.Api.Data;

public interface IApplicationDbContext
{
    DbSet<Event> Events { get; }
    DbSet<EventRegistration> EventRegistrations { get; }
    DbSet<Student> Students { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<Teacher> Teachers { get; }
    DbSet<TeacherSubject> TeacherSubjects { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<StudentFeedback> StudentFeedbacks { get; }
    DbSet<EventComment> EventComments { get; }
    DbSet<SmartCard> SmartCards { get; }
    DbSet<SmartIDScanLog> SmartIDScanLogs { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}