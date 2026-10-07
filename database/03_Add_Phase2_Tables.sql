USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ----------------------------------------------------------------------------
   11. PASSWORD RESET (dedicated table, separate from AspNetUserTokens, so we
       control expiry, one-time use and audit independently of Identity's
       default token provider)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PasswordResetRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetRequests
    (
        Id              bigint        IDENTITY(1,1) NOT NULL,
        UserId          nvarchar(450) NOT NULL,
        Token           nvarchar(256) NOT NULL,
        ExpiresAt       datetime2(7)  NOT NULL,
        UsedAt          datetime2(7)  NULL,
        RequestedFromIp nvarchar(45)  NULL,
        CreatedAt       datetime2(7)  NOT NULL CONSTRAINT DF_PasswordResetRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PasswordResetRequests PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_PasswordResetRequests_Token UNIQUE NONCLUSTERED (Token),
        CONSTRAINT FK_PasswordResetRequests_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
        CONSTRAINT CK_PasswordResetRequests_Expiry CHECK (ExpiresAt > CreatedAt)
    );

    CREATE NONCLUSTERED INDEX IX_PasswordResetRequests_UserId_ExpiresAt
        ON dbo.PasswordResetRequests (UserId, ExpiresAt) INCLUDE (UsedAt);
END
GO

/* ----------------------------------------------------------------------------
   12. AUDIT LOG (generic, covers marks, approvals, announcements, etc.)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        Id         bigint        IDENTITY(1,1) NOT NULL,
        UserId     nvarchar(450) NULL,
        Action     nvarchar(100) NOT NULL,   -- e.g. 'Mark.Updated', 'Registration.Approved'
        EntityType nvarchar(100) NOT NULL,   -- e.g. 'Mark', 'StudentRegistrationRequest'
        EntityId   nvarchar(100) NULL,
        OldValue   nvarchar(max) NULL,
        NewValue   nvarchar(max) NULL,
        IpAddress  nvarchar(45)  NULL,
        CreatedAt  datetime2(7)  NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AuditLogs_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE SET NULL
    );

    CREATE NONCLUSTERED INDEX IX_AuditLogs_EntityType_EntityId ON dbo.AuditLogs (EntityType, EntityId);
    CREATE NONCLUSTERED INDEX IX_AuditLogs_UserId_CreatedAt     ON dbo.AuditLogs (UserId, CreatedAt DESC);
    CREATE NONCLUSTERED INDEX IX_AuditLogs_CreatedAt            ON dbo.AuditLogs (CreatedAt DESC);
END
GO

/* ----------------------------------------------------------------------------
   13. ATTENDANCE (daily, one row per student per day)
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Attendance', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Attendance
    (
        Id                 int           IDENTITY(1,1) NOT NULL,
        StudentId          int           NOT NULL,
        AttendanceDate     date          NOT NULL,
        Status             nvarchar(20)  NOT NULL CONSTRAINT DF_Attendance_Status DEFAULT (N'Present'),
        RecordedByTeacherId int          NULL,
        Remark             nvarchar(300) NULL,
        CreatedAt          datetime2(7)  NOT NULL CONSTRAINT DF_Attendance_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt          datetime2(7)  NULL,
        CONSTRAINT PK_Attendance PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Attendance_Student_Date UNIQUE NONCLUSTERED (StudentId, AttendanceDate),
        CONSTRAINT FK_Attendance_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Attendance_Teachers_RecordedByTeacherId FOREIGN KEY (RecordedByTeacherId)
            REFERENCES dbo.Teachers (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Attendance_Status
            CHECK (Status IN (N'Present', N'Absent', N'Late', N'Excused'))
    );

    CREATE NONCLUSTERED INDEX IX_Attendance_AttendanceDate ON dbo.Attendance (AttendanceDate);
    CREATE NONCLUSTERED INDEX IX_Attendance_StudentId      ON dbo.Attendance (StudentId) INCLUDE (AttendanceDate, Status);
END
GO

/* ----------------------------------------------------------------------------
   14. PARENT / GUARDIAN ACCOUNTS
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Guardians', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Guardians
    (
        Id          int           IDENTITY(1,1) NOT NULL,
        UserId      nvarchar(450) NOT NULL,
        PhoneNumber nvarchar(30)  NULL,
        CreatedAt   datetime2(7)  NOT NULL CONSTRAINT DF_Guardians_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Guardians PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Guardians_UserId UNIQUE NONCLUSTERED (UserId),
        CONSTRAINT FK_Guardians_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.StudentGuardians', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentGuardians
    (
        Id               int           IDENTITY(1,1) NOT NULL,
        StudentId        int           NOT NULL,
        GuardianId       int           NOT NULL,
        Relationship     nvarchar(50)  NULL,   -- e.g. 'Mother', 'Father', 'Guardian'
        IsPrimaryContact bit           NOT NULL CONSTRAINT DF_StudentGuardians_IsPrimaryContact DEFAULT (0),
        CreatedAt        datetime2(7)  NOT NULL CONSTRAINT DF_StudentGuardians_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_StudentGuardians PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_StudentGuardians_Pair UNIQUE NONCLUSTERED (StudentId, GuardianId),
        CONSTRAINT FK_StudentGuardians_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_StudentGuardians_Guardians_GuardianId FOREIGN KEY (GuardianId)
            REFERENCES dbo.Guardians (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_StudentGuardians_GuardianId ON dbo.StudentGuardians (GuardianId);
END
GO

/* ----------------------------------------------------------------------------
   15. TIMETABLE
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.TimetableSlots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TimetableSlots
    (
        Id           int          IDENTITY(1,1) NOT NULL,
        SectionId    int          NOT NULL,
        SubjectId    int          NOT NULL,
        TeacherId    int          NOT NULL,
        DayOfWeek    tinyint      NOT NULL,   -- 1 = Monday ... 7 = Sunday
        PeriodNumber tinyint      NOT NULL,
        StartTime    time(0)      NOT NULL,
        EndTime      time(0)      NOT NULL,
        AcademicYear nvarchar(9)  NOT NULL CONSTRAINT DF_TimetableSlots_AcademicYear DEFAULT (N'2026-2027'),
        CreatedAt    datetime2(7) NOT NULL CONSTRAINT DF_TimetableSlots_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_TimetableSlots PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_TimetableSlots_Slot UNIQUE NONCLUSTERED (SectionId, DayOfWeek, PeriodNumber, AcademicYear),
        CONSTRAINT FK_TimetableSlots_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TimetableSlots_Subjects_SubjectId FOREIGN KEY (SubjectId)
            REFERENCES dbo.Subjects (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TimetableSlots_Teachers_TeacherId FOREIGN KEY (TeacherId)
            REFERENCES dbo.Teachers (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_TimetableSlots_DayOfWeek CHECK (DayOfWeek BETWEEN 1 AND 7),
        CONSTRAINT CK_TimetableSlots_Times CHECK (EndTime > StartTime),
        CONSTRAINT CK_TimetableSlots_AcademicYear
            CHECK (AcademicYear LIKE N'[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]')
    );

    CREATE NONCLUSTERED INDEX IX_TimetableSlots_TeacherId_DayOfWeek ON dbo.TimetableSlots (TeacherId, DayOfWeek);
END
GO

/* ----------------------------------------------------------------------------
   16. FEES
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.FeeInvoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FeeInvoices
    (
        Id           int           IDENTITY(1,1) NOT NULL,
        StudentId    int           NOT NULL,
        AcademicYear nvarchar(9)   NOT NULL,
        Term         nvarchar(20)  NULL,
        [Description] nvarchar(200) NOT NULL,
        AmountDue    decimal(10,2) NOT NULL,
        DueDate      date          NULL,
        IsVoided     bit           NOT NULL CONSTRAINT DF_FeeInvoices_IsVoided DEFAULT (0),
        CreatedAt    datetime2(7)  NOT NULL CONSTRAINT DF_FeeInvoices_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FeeInvoices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_FeeInvoices_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT CK_FeeInvoices_AmountDue CHECK (AmountDue > 0)
    );

    CREATE NONCLUSTERED INDEX IX_FeeInvoices_StudentId ON dbo.FeeInvoices (StudentId);
END
GO

IF OBJECT_ID(N'dbo.FeePayments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FeePayments
    (
        Id               int           IDENTITY(1,1) NOT NULL,
        InvoiceId        int           NOT NULL,
        AmountPaid       decimal(10,2) NOT NULL,
        PaymentMethod    nvarchar(30)  NULL,
        RecordedByUserId nvarchar(450) NULL,
        PaidAt           datetime2(7)  NOT NULL CONSTRAINT DF_FeePayments_PaidAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FeePayments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_FeePayments_FeeInvoices_InvoiceId FOREIGN KEY (InvoiceId)
            REFERENCES dbo.FeeInvoices (Id) ON DELETE CASCADE,
        CONSTRAINT FK_FeePayments_AspNetUsers_RecordedByUserId FOREIGN KEY (RecordedByUserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE SET NULL,
        CONSTRAINT CK_FeePayments_AmountPaid CHECK (AmountPaid > 0)
    );

    CREATE NONCLUSTERED INDEX IX_FeePayments_InvoiceId ON dbo.FeePayments (InvoiceId);
END
GO

/* ----------------------------------------------------------------------------
   17. BULK IMPORT TRACKING
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.BulkImportBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BulkImportBatches
    (
        Id               int           IDENTITY(1,1) NOT NULL,
        ImportedByUserId nvarchar(450) NOT NULL,
        FileName         nvarchar(255) NOT NULL,
        TotalRows        int           NOT NULL,
        SuccessCount     int           NOT NULL CONSTRAINT DF_BulkImportBatches_SuccessCount DEFAULT (0),
        FailureCount     int           NOT NULL CONSTRAINT DF_BulkImportBatches_FailureCount DEFAULT (0),
        ErrorLog         nvarchar(max) NULL,
        Status           nvarchar(20)  NOT NULL CONSTRAINT DF_BulkImportBatches_Status DEFAULT (N'Processing'),
        CreatedAt        datetime2(7)  NOT NULL CONSTRAINT DF_BulkImportBatches_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CompletedAt      datetime2(7)  NULL,
        CONSTRAINT PK_BulkImportBatches PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_BulkImportBatches_AspNetUsers_ImportedByUserId FOREIGN KEY (ImportedByUserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE NO ACTION,
        CONSTRAINT CK_BulkImportBatches_Status
            CHECK (Status IN (N'Processing', N'Completed', N'Failed'))
    );
END
GO

PRINT N'--- Phase 2 tables created: PasswordResetRequests, AuditLogs, Attendance, Guardians, StudentGuardians, TimetableSlots, FeeInvoices, FeePayments, BulkImportBatches ---';