using HaladeHighSchool.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HaladeHighSchool.Api.Data;


public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentTypeWeight> AssessmentTypeWeights => Set<AssessmentTypeWeight>();
    public DbSet<Mark> Marks => Set<Mark>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<StudentRegistrationRequest> StudentRegistrationRequests => Set<StudentRegistrationRequest>();
    public DbSet<PasswordChangeLog> PasswordChangeLogs => Set<PasswordChangeLog>();
      public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();
      public DbSet<Attendance> Attendance => Set<Attendance>();
      public DbSet<Event> Events => Set<Event>();
      public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
      public DbSet<Notification> Notifications => Set<Notification>();
      public DbSet<StudentFeedback> StudentFeedbacks => Set<StudentFeedback>();
      public DbSet<EventComment> EventComments => Set<EventComment>();
      public DbSet<SmartCard> SmartCards => Set<SmartCard>();
      public DbSet<SmartIDScanLog> SmartIDScanLogs => Set<SmartIDScanLog>();
      public DbSet<FeeInvoice> FeeInvoices => Set<FeeInvoice>();
      public DbSet<FeePayment> FeePayments => Set<FeePayment>();

    /// <summary>Weighted report card rows produced by vw_StudentSubjectPerformance.</summary>
    public DbSet<StudentSubjectPerformance> StudentSubjectPerformances => Set<StudentSubjectPerformance>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureAcademicStructure(builder);
        ConfigurePeople(builder);
        ConfigureContent(builder);
        ConfigureAssessment(builder);
        ConfigureCommunication(builder);
        ConfigureFeedbackAndComments(builder);
        ConfigureViews(builder);
        ConfigureSmartId(builder);
        ConfigureFees(builder);
    }

    private static void ConfigureSmartId(ModelBuilder builder)
    {
        builder.Entity<SmartCard>(entity =>
        {
            entity.ToTable("SmartCards");
            entity.HasKey(card => card.CardId);
            entity.Property(card => card.CardId).HasDefaultValueSql("NEWID()");
            entity.Property(card => card.UserId).HasMaxLength(450).IsRequired();
            entity.Property(card => card.CardUID).HasMaxLength(100).IsRequired();
            entity.Property(card => card.QrTokenHash).HasMaxLength(512).IsRequired();
            entity.Property(card => card.CardType).HasMaxLength(20).IsRequired();
            entity.Property(card => card.Status).HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();
            entity.Property(card => card.IssuedDate).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(card => card.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(card => card.UserId).IsUnique().HasDatabaseName("UQ_SmartCards_UserId");
            entity.HasIndex(card => card.CardUID).IsUnique().HasDatabaseName("UQ_SmartCards_CardUID");
            entity.HasOne(card => card.User)
                .WithOne(user => user.SmartCard)
                .HasForeignKey<SmartCard>(card => card.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SmartIDScanLog>(entity =>
        {
            entity.ToTable("SmartIDScanLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Id).ValueGeneratedOnAdd();
            entity.Property(log => log.ScannerUserId).HasMaxLength(450).IsRequired();
            entity.Property(log => log.ScannedUserId).HasMaxLength(450);
            entity.Property(log => log.ScanLocation).HasMaxLength(200);
            entity.Property(log => log.ScanStatus).HasMaxLength(20).IsRequired();
            entity.Property(log => log.ScanTimestamp).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(log => log.ScanTimestamp).HasDatabaseName("IX_SmartIDScanLogs_ScanTimestamp");
        });
    }

    private static void ConfigureFees(ModelBuilder builder)
    {
        builder.Entity<FeeInvoice>(entity =>
        {
            entity.ToTable("FeeInvoices");
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.AcademicYear).HasMaxLength(9).IsRequired();
            entity.Property(invoice => invoice.Term).HasMaxLength(20);
            entity.Property(invoice => invoice.Description).HasMaxLength(200).IsRequired();
            entity.Property(invoice => invoice.AmountDue).HasPrecision(10, 2);
            entity.Property(invoice => invoice.IsVoided).HasDefaultValue(false);
            entity.Property(invoice => invoice.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(invoice => invoice.Student)
                .WithMany()
                .HasForeignKey(invoice => invoice.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(invoice => invoice.Payments)
                .WithOne(payment => payment.Invoice)
                .HasForeignKey(payment => payment.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FeePayment>(entity =>
        {
            entity.ToTable("FeePayments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.AmountPaid).HasPrecision(10, 2);
            entity.Property(payment => payment.PaymentMethod).HasMaxLength(30);
            entity.Property(payment => payment.RecordedByUserId).HasMaxLength(450);
            entity.Property(payment => payment.PaidAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(payment => payment.InvoiceId).HasDatabaseName("IX_FeePayments_InvoiceId");
        });
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            entity.Property(u => u.ProfileImageUrl).HasMaxLength(500);
            entity.Property(u => u.PhotoUrl).HasMaxLength(500);
            entity.Property(u => u.DigitalSignatureUrl).HasMaxLength(500);
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(u => u.IsActive).HasDatabaseName("IX_AspNetUsers_IsActive");
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.Property(t => t.Token).HasMaxLength(256).IsRequired();
            entity.Property(t => t.ReplacedByToken).HasMaxLength(256);
            entity.Property(t => t.CreatedByIp).HasMaxLength(45);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Ignore(t => t.IsActive);
            entity.HasIndex(t => t.Token).IsUnique().HasDatabaseName("UQ_RefreshTokens_Token");

            entity.HasOne(t => t.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PasswordResetRequest>(entity =>
        {
            entity.ToTable("PasswordResetRequests");
            entity.Property(r => r.UserId).HasMaxLength(450).IsRequired();
            entity.Property(r => r.Token).HasMaxLength(256).IsRequired();
            entity.Property(r => r.RequestedFromIp).HasMaxLength(45);
            entity.Property(r => r.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(r => r.Token).IsUnique().HasDatabaseName("UQ_PasswordResetRequests_Token");
            entity.HasIndex(r => new { r.UserId, r.ExpiresAt })
                  .HasDatabaseName("IX_PasswordResetRequests_UserId_ExpiresAt");

            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Attendance>(entity =>
        {
            entity.ToTable("Attendance");
            entity.Property(a => a.Status).HasMaxLength(20).IsRequired();
            entity.Property(a => a.Remark).HasMaxLength(300);
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(a => new { a.StudentId, a.AttendanceDate })
                  .IsUnique()
                  .HasDatabaseName("UQ_Attendance_Student_Date");
            entity.HasIndex(a => a.AttendanceDate)
                  .HasDatabaseName("IX_Attendance_AttendanceDate");
            entity.HasIndex(a => a.StudentId)
                  .HasDatabaseName("IX_Attendance_StudentId");

            entity.HasOne(a => a.Student)
                  .WithMany()
                  .HasForeignKey(a => a.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.RecordedByTeacher)
                  .WithMany()
                  .HasForeignKey(a => a.RecordedByTeacherId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAcademicStructure(ModelBuilder builder)
    {
        builder.Entity<GradeLevel>(entity =>
        {
            entity.ToTable("GradeLevels");
            entity.Property(g => g.Name).HasMaxLength(50).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(250);
            entity.Property(g => g.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(g => g.Name).IsUnique().HasDatabaseName("UQ_GradeLevels_Name");
            entity.HasIndex(g => g.Level).IsUnique().HasDatabaseName("UQ_GradeLevels_Level");
        });

        builder.Entity<Section>(entity =>
        {
            entity.ToTable("Sections");
            entity.Property(s => s.Name).HasMaxLength(50).IsRequired();
            entity.Property(s => s.Code).HasMaxLength(10).IsRequired();
            entity.Property(s => s.Capacity).HasDefaultValue(40);
            entity.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(s => s.Name).IsUnique().HasDatabaseName("UQ_Sections_Name");
            entity.HasIndex(s => s.Code).IsUnique().HasDatabaseName("UQ_Sections_Code");
        });

        builder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects");
            entity.Property(s => s.SubjectName).HasMaxLength(150).IsRequired();
            entity.Property(s => s.Code).HasMaxLength(20).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(500);
            entity.Property(s => s.CreditHours).HasDefaultValue(3);
            entity.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(s => s.Code).IsUnique().HasDatabaseName("UQ_Subjects_Code");
            entity.HasIndex(s => new { s.SubjectName, s.GradeLevelId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Subjects_Name_Grade");

            entity.HasOne(s => s.GradeLevel)
                  .WithMany(g => g.Subjects)
                  .HasForeignKey(s => s.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePeople(ModelBuilder builder)
    {
        builder.Entity<Teacher>(entity =>
        {
            entity.ToTable("Teachers");
            entity.Property(t => t.EmployeeId).HasMaxLength(30).IsRequired();
            entity.Property(t => t.Specialization).HasMaxLength(150);
            entity.Property(t => t.Qualification).HasMaxLength(150);
            entity.Property(t => t.PhoneNumber).HasMaxLength(30);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(t => t.EmployeeId).IsUnique().HasDatabaseName("UQ_Teachers_EmployeeId");

            // Filtered unique index: at most one teacher per login, many rows without a login.
            entity.HasIndex(t => t.UserId)
                  .IsUnique()
                  .HasFilter("[UserId] IS NOT NULL")
                  .HasDatabaseName("UQ_Teachers_UserId");

            entity.HasOne(t => t.User)
                  .WithOne(u => u.Teacher)
                  .HasForeignKey<Teacher>(t => t.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.Property(s => s.StudentIdNumber).HasMaxLength(30).IsRequired();
            entity.Property(s => s.Gender).HasMaxLength(10);
            entity.Property(s => s.GuardianName).HasMaxLength(150);
            entity.Property(s => s.GuardianPhone).HasMaxLength(30);
            entity.Property(s => s.EmergencyContact).HasMaxLength(100);
            entity.Property(s => s.BloodGroup).HasMaxLength(10);
            entity.Property(s => s.Address).HasMaxLength(250);
            entity.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(s => s.StudentIdNumber).IsUnique().HasDatabaseName("UQ_Students_StudentIdNumber");

            entity.HasIndex(s => s.UserId)
                  .IsUnique()
                  .HasFilter("[UserId] IS NOT NULL")
                  .HasDatabaseName("UQ_Students_UserId");

            entity.HasOne(s => s.GradeLevel)
                  .WithMany(g => g.Students)
                  .HasForeignKey(s => s.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Section)
                  .WithMany(sec => sec.Students)
                  .HasForeignKey(s => s.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.User)
                  .WithOne(u => u.Student)
                  .HasForeignKey<Student>(s => s.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Guardian>(entity =>
        {
            entity.ToTable("Guardians");
            entity.Property(g => g.UserId).HasMaxLength(450).IsRequired();
            entity.Property(g => g.PhoneNumber).HasMaxLength(30);
            entity.Property(g => g.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(g => g.UserId).IsUnique().HasDatabaseName("UQ_Guardians_UserId");

            entity.HasOne(g => g.User)
                  .WithOne(u => u.Guardian)
                  .HasForeignKey<Guardian>(g => g.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StudentGuardian>(entity =>
        {
            entity.ToTable("StudentGuardians");
            entity.Property(sg => sg.Relationship).HasMaxLength(50);
            entity.Property(sg => sg.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(sg => new { sg.GuardianId, sg.StudentId }).IsUnique().HasDatabaseName("UQ_StudentGuardians_Pair");
            entity.HasIndex(sg => sg.GuardianId).HasDatabaseName("IX_StudentGuardians_GuardianId");

            entity.HasOne(sg => sg.Student)
                  .WithMany(s => s.StudentGuardians)
                  .HasForeignKey(sg => sg.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sg => sg.Guardian)
                  .WithMany(g => g.StudentGuardians)
                  .HasForeignKey(sg => sg.GuardianId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StudentRegistrationRequest>(entity =>
        {
            entity.ToTable("StudentRegistrationRequests");
            entity.Property(r => r.FullName).HasMaxLength(150).IsRequired();
            entity.Property(r => r.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(r => r.PhotoUrl).HasMaxLength(500);
            entity.Property(r => r.RequestedRole).HasMaxLength(20).HasDefaultValue("Student");
            entity.Property(r => r.Status).HasMaxLength(20).IsRequired();
            entity.Property(r => r.SubmittedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(r => r.ReviewedByUserId).HasMaxLength(450);
            entity.Property(r => r.ReviewNote).HasMaxLength(300);
            entity.Property(r => r.IssuedEmail).HasMaxLength(256);

            // Matches the filtered unique index: one open request per contact address.
            entity.HasIndex(r => r.ContactEmail)
                  .IsUnique()
                  .HasFilter("[Status] = N'Pending'")
                  .HasDatabaseName("UQ_StudentRegistrationRequests_PendingContactEmail");

            entity.HasIndex(r => new { r.Status, r.SubmittedAt })
                  .HasDatabaseName("IX_StudentRegistrationRequests_Status_SubmittedAt");

            entity.HasOne(r => r.GradeLevel)
                  .WithMany()
                  .HasForeignKey(r => r.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Section)
                  .WithMany()
                  .HasForeignKey(r => r.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CreatedStudent)
                  .WithMany()
                  .HasForeignKey(r => r.CreatedStudentId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PasswordChangeLog>(entity =>
        {
            entity.ToTable("PasswordChangeLogs");
            entity.Property(l => l.Outcome).HasMaxLength(20).IsRequired();
            entity.Property(l => l.FailureReason).HasMaxLength(300);
            entity.Property(l => l.IpAddress).HasMaxLength(45);
            entity.Property(l => l.UserAgent).HasMaxLength(400);
            entity.Property(l => l.ChangedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(l => new { l.UserId, l.ChangedAt })
                  .HasDatabaseName("IX_PasswordChangeLogs_UserId_ChangedAt");

            entity.HasOne(l => l.User)
                  .WithMany()
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeacherSubject>(entity =>
        {
            entity.ToTable("TeacherSubjects");
            entity.Property(ts => ts.AcademicYear).HasMaxLength(9).IsRequired();
            entity.Property(ts => ts.AssignedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(ts => new { ts.TeacherId, ts.SubjectId, ts.SectionId, ts.AcademicYear })
                  .IsUnique()
                  .HasDatabaseName("UQ_TeacherSubjects_Assignment");

            entity.HasOne(ts => ts.Teacher)
                  .WithMany(t => t.TeacherSubjects)
                  .HasForeignKey(ts => ts.TeacherId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ts => ts.Subject)
                  .WithMany(s => s.TeacherSubjects)
                  .HasForeignKey(ts => ts.SubjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ts => ts.Section)
                  .WithMany(s => s.TeacherSubjects)
                  .HasForeignKey(ts => ts.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureContent(ModelBuilder builder)
    {
        builder.Entity<Lesson>(entity =>
        {
            entity.ToTable("Lessons");
            entity.Property(l => l.Title).HasMaxLength(200).IsRequired();
            entity.Property(l => l.FileUrl).HasMaxLength(500);
            entity.Property(l => l.FileName).HasMaxLength(255);
            entity.Property(l => l.ContentType).HasMaxLength(100);
            entity.Property(l => l.IsPublished).HasDefaultValue(true);
            entity.Property(l => l.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(l => l.Subject)
                  .WithMany(s => s.Lessons)
                  .HasForeignKey(l => l.SubjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Teacher)
                  .WithMany(t => t.Lessons)
                  .HasForeignKey(l => l.TeacherId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.Section)
                  .WithMany()
                  .HasForeignKey(l => l.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAssessment(ModelBuilder builder)
    {
        builder.Entity<AssessmentTypeWeight>(entity =>
        {
            entity.ToTable("AssessmentTypes");
            entity.HasKey(a => a.Name);
            entity.Property(a => a.Name).HasMaxLength(20);
            entity.Property(a => a.DisplayName).HasMaxLength(50).IsRequired();
            entity.Property(a => a.WeightPercentage).HasPrecision(5, 2);
        });

        builder.Entity<Assessment>(entity =>
        {
            entity.ToTable("Assessments");
            entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
            entity.Property(a => a.CustomTypeTitle).HasMaxLength(100);
            entity.Property(a => a.MaxScore).HasPrecision(6, 2);
            entity.Property(a => a.AcademicYear).HasMaxLength(9).IsRequired();
            entity.Property(a => a.IsActive).HasDefaultValue(true);
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // Stored as text to satisfy FK_Assessments_AssessmentTypes_AssessmentType.
            entity.Property(a => a.AssessmentType)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            entity.HasOne(a => a.Subject)
                  .WithMany(s => s.Assessments)
                  .HasForeignKey(a => a.SubjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Section)
                  .WithMany()
                  .HasForeignKey(a => a.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Teacher)
                  .WithMany(t => t.Assessments)
                  .HasForeignKey(a => a.TeacherId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Mark>(entity =>
        {
            // TR_Marks_Validate is an AFTER trigger. SQL Server cannot use an OUTPUT
            // clause on a table with triggers, so EF must be told it exists or every
            // SaveChanges against Marks fails at runtime.
            entity.ToTable("Marks", t => t.HasTrigger("TR_Marks_Validate"));

            entity.Property(m => m.Score).HasPrecision(6, 2);
            entity.Property(m => m.Remark).HasMaxLength(300);
            entity.Property(m => m.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(m => new { m.StudentId, m.AssessmentId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Marks_Student_Assessment");

            entity.HasOne(m => m.Student)
                  .WithMany(s => s.Marks)
                  .HasForeignKey(m => m.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Assessment)
                  .WithMany(a => a.Marks)
                  .HasForeignKey(m => m.AssessmentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // NO ACTION in SQL to avoid a multiple cascade path through Assessments.
            entity.HasOne(m => m.Subject)
                  .WithMany(s => s.Marks)
                  .HasForeignKey(m => m.SubjectId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.EnteredByTeacher)
                  .WithMany()
                  .HasForeignKey(m => m.EnteredByTeacherId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCommunication(ModelBuilder builder)
    {
        builder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcements");
            entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Content).IsRequired();
            entity.Property(a => a.TargetRole).HasMaxLength(20).HasDefaultValue("All").IsRequired();
            entity.Property(a => a.IsPublished).HasDefaultValue(true);
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(a => a.GradeLevel)
                  .WithMany()
                  .HasForeignKey(a => a.GradeLevelId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Section)
                  .WithMany()
                  .HasForeignKey(a => a.SectionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(a => a.CreatedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasMaxLength(100);
            entity.Property(s => s.Value).HasMaxLength(500);
            entity.Property(s => s.Description).HasMaxLength(300);
            entity.Property(s => s.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

            builder.Entity<Event>(entity =>
            {
                  entity.ToTable("Events");
                  entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                  entity.Property(e => e.BannerImageUrl).HasMaxLength(500);
                  entity.Property(e => e.Category).HasMaxLength(50).HasDefaultValue("General").IsRequired();
                  entity.Property(e => e.Venue).HasMaxLength(200);
                  entity.Property(e => e.Organizer).HasMaxLength(150);
                  entity.Property(e => e.TargetRole).HasMaxLength(20).HasDefaultValue("All").IsRequired();
                  entity.Property(e => e.TargetGradeIdsCsv).HasMaxLength(1000);
                  entity.Property(e => e.TargetSectionIdsCsv).HasMaxLength(1000);
                  entity.Property(e => e.StatusOverride).HasMaxLength(20);
                  entity.Property(e => e.RegistrationDeadline).HasColumnName("RegistrationDeadline");
                  entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                  entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                  entity.HasIndex(e => e.StartsAt).HasDatabaseName("IX_Events_StartDate");
                  entity.HasIndex(e => e.Category).HasDatabaseName("IX_Events_Category");
                  entity.HasIndex(e => e.IsPublished).HasDatabaseName("IX_Events_IsActive");

                  entity.HasOne(e => e.GradeLevel).WithMany().HasForeignKey(e => e.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
                  entity.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
                  entity.HasOne(e => e.OrganizedByUser).WithMany().HasForeignKey(e => e.OrganizedByUserId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<EventRegistration>(entity =>
            {
                  entity.ToTable("EventRegistrations");
                  entity.Property(r => r.Status).HasMaxLength(20).HasDefaultValue(EventRegistrationStatuses.Confirmed).IsRequired();
                  entity.Property(r => r.AttendanceStatus).HasMaxLength(20).HasDefaultValue(EventAttendanceStatuses.Pending).IsRequired();
                  entity.Property(r => r.RegisteredAt).HasDefaultValueSql("SYSUTCDATETIME()");
                  entity.HasIndex(r => new { r.EventId, r.StudentId }).IsUnique().HasDatabaseName("UQ_EventRegistrations_Event_Student");
                  entity.HasOne(r => r.Event).WithMany(e => e.Registrations).HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Cascade);
                  entity.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Notification>(entity =>
            {
                  entity.ToTable("Notifications");
                  entity.Property(n => n.UserId).HasMaxLength(450).IsRequired();
                  entity.Property(n => n.Title).HasMaxLength(200).IsRequired();
                  entity.Property(n => n.Message).HasMaxLength(1000).IsRequired();
                  entity.Property(n => n.Type).HasMaxLength(30).IsRequired();
                  entity.Property(n => n.TargetUrl).HasMaxLength(500).IsRequired();
                  entity.Property(n => n.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                  entity.HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt })
                      .HasDatabaseName("IX_Notifications_User_Read_Created");
                  entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            });
    }

    private static void ConfigureFeedbackAndComments(ModelBuilder builder)
    {
        builder.Entity<StudentFeedback>(entity =>
        {
            entity.ToTable("StudentFeedbacks");
            entity.Property(feedback => feedback.Category).HasMaxLength(30).IsRequired();
            entity.Property(feedback => feedback.Message).HasMaxLength(4000).IsRequired();
            entity.Property(feedback => feedback.Status)
                  .HasMaxLength(30)
                  .HasDefaultValue(StudentFeedbackStatuses.PendingReview)
                  .IsRequired();
            entity.Property(feedback => feedback.SubmittedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(feedback => new { feedback.Status, feedback.SubmittedAt })
                  .HasDatabaseName("IX_StudentFeedbacks_Status_SubmittedAt");
            entity.HasOne(feedback => feedback.Student)
                  .WithMany()
                  .HasForeignKey(feedback => feedback.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventComment>(entity =>
        {
            entity.ToTable("EventComments");
            entity.Property(comment => comment.UserId).HasMaxLength(450);
            entity.Property(comment => comment.UserName).HasMaxLength(150).IsRequired();
            entity.Property(comment => comment.UserRole).HasMaxLength(20).IsRequired();
            entity.Property(comment => comment.CommentText).HasMaxLength(2000).IsRequired();
            entity.Property(comment => comment.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(comment => new { comment.EventId, comment.CreatedAt })
                  .HasDatabaseName("IX_EventComments_Event_CreatedAt");
            entity.HasIndex(comment => comment.ParentCommentId)
                  .HasDatabaseName("IX_EventComments_ParentCommentId");
            entity.HasOne(comment => comment.Event)
                  .WithMany(calendarEvent => calendarEvent.Comments)
                  .HasForeignKey(comment => comment.EventId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(comment => comment.User)
                  .WithMany()
                  .HasForeignKey(comment => comment.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(comment => comment.ParentComment)
                  .WithMany(parent => parent.Replies)
                  .HasForeignKey(comment => comment.ParentCommentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(comment => comment.Student)
                  .WithMany()
                  .HasForeignKey(comment => comment.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureViews(ModelBuilder builder)
    {
        builder.Entity<StudentSubjectPerformance>(entity =>
        {
            entity.HasNoKey().ToView("vw_StudentSubjectPerformance");
            entity.Property(p => p.QuizScore).HasPrecision(6, 2);
            entity.Property(p => p.TestScore).HasPrecision(6, 2);
            entity.Property(p => p.MidExamScore).HasPrecision(6, 2);
            entity.Property(p => p.FinalExamScore).HasPrecision(6, 2);
            entity.Property(p => p.TotalScore).HasPrecision(6, 2);
        });
    }
}
