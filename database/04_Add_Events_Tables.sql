USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ----------------------------------------------------------------------------
   Events — school events (sports day, science fair, graduation, open house).
   Distinct from Announcements: this has a banner image, capacity, a venue,
   a start/end time, and real RSVPs via EventRegistrations, rather than being
   a text-only notice.
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Events', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Events
    (
        Id                  int           IDENTITY(1,1) NOT NULL,
        Title               nvarchar(200) NOT NULL,
        [Description]       nvarchar(max) NULL,
        BannerImageUrl      nvarchar(500) NULL,
        Venue               nvarchar(200) NULL,
        StartsAt            datetime2(7)  NOT NULL,
        EndsAt               datetime2(7)  NULL,
        TargetRole          nvarchar(20)  NOT NULL CONSTRAINT DF_Events_TargetRole DEFAULT (N'All'),
        GradeLevelId        int           NULL,   -- NULL = every grade
        SectionId           int           NULL,   -- NULL = every section
        MaxStudents         int           NULL,   -- NULL = unlimited
        MaxGuestsPerStudent int           NOT NULL CONSTRAINT DF_Events_MaxGuestsPerStudent DEFAULT (0),
        OrganizedByUserId   nvarchar(450) NULL,
        IsPublished         bit           NOT NULL CONSTRAINT DF_Events_IsPublished DEFAULT (1),
        IsCancelled         bit           NOT NULL CONSTRAINT DF_Events_IsCancelled DEFAULT (0),
        CreatedAt           datetime2(7)  NOT NULL CONSTRAINT DF_Events_CreatedAt   DEFAULT (SYSUTCDATETIME()),
        UpdatedAt           datetime2(7)  NULL,
        CONSTRAINT PK_Events PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Events_GradeLevels_GradeLevelId FOREIGN KEY (GradeLevelId)
            REFERENCES dbo.GradeLevels (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Events_Sections_SectionId FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Events_AspNetUsers_OrganizedByUserId FOREIGN KEY (OrganizedByUserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE SET NULL,
        CONSTRAINT CK_Events_TargetRole
            CHECK (TargetRole IN (N'All', N'Admin', N'Teacher', N'Student')),
        CONSTRAINT CK_Events_MaxStudents CHECK (MaxStudents IS NULL OR MaxStudents > 0),
        CONSTRAINT CK_Events_MaxGuestsPerStudent CHECK (MaxGuestsPerStudent >= 0),
        CONSTRAINT CK_Events_Times CHECK (EndsAt IS NULL OR EndsAt > StartsAt)
    );

    CREATE NONCLUSTERED INDEX IX_Events_StartsAt ON dbo.Events (StartsAt);
    CREATE NONCLUSTERED INDEX IX_Events_TargetRole_StartsAt
        ON dbo.Events (TargetRole, StartsAt) INCLUDE (Title, IsPublished, IsCancelled, GradeLevelId, SectionId);
END
GO

IF COL_LENGTH(N'dbo.Events', N'Category') IS NULL
    ALTER TABLE dbo.Events ADD Category nvarchar(50) NOT NULL CONSTRAINT DF_Events_Category DEFAULT (N'General');
GO

IF COL_LENGTH(N'dbo.Events', N'Organizer') IS NULL
    ALTER TABLE dbo.Events ADD Organizer nvarchar(150) NULL;
GO

IF COL_LENGTH(N'dbo.Events', N'RegistrationDeadline') IS NULL
    ALTER TABLE dbo.Events ADD RegistrationDeadline datetime2(7) NULL;
GO

IF COL_LENGTH(N'dbo.Events', N'StatusOverride') IS NULL
    ALTER TABLE dbo.Events ADD StatusOverride nvarchar(20) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Events_Category' AND object_id = OBJECT_ID(N'dbo.Events'))
    CREATE NONCLUSTERED INDEX IX_Events_Category ON dbo.Events (Category);
GO

/* ----------------------------------------------------------------------------
   EventRegistrations — one row per student's RSVP, with how many family
   guests they're bringing (capped by Events.MaxGuestsPerStudent).
   ---------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.EventRegistrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventRegistrations
    (
        Id           int           IDENTITY(1,1) NOT NULL,
        EventId      int           NOT NULL,
        StudentId    int           NOT NULL,
        GuestCount   int           NOT NULL CONSTRAINT DF_EventRegistrations_GuestCount DEFAULT (0),
        Status       nvarchar(20)  NOT NULL CONSTRAINT DF_EventRegistrations_Status DEFAULT (N'Confirmed'),
        RegisteredAt datetime2(7)  NOT NULL CONSTRAINT DF_EventRegistrations_RegisteredAt DEFAULT (SYSUTCDATETIME()),
        CancelledAt  datetime2(7)  NULL,
        CONSTRAINT PK_EventRegistrations PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_EventRegistrations_Event_Student UNIQUE NONCLUSTERED (EventId, StudentId),
        CONSTRAINT FK_EventRegistrations_Events_EventId FOREIGN KEY (EventId)
            REFERENCES dbo.Events (Id) ON DELETE CASCADE,
        CONSTRAINT FK_EventRegistrations_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT CK_EventRegistrations_GuestCount CHECK (GuestCount >= 0),
        CONSTRAINT CK_EventRegistrations_Status
            CHECK (Status IN (N'Confirmed', N'Cancelled', N'Waitlisted'))
    );

    CREATE NONCLUSTERED INDEX IX_EventRegistrations_EventId ON dbo.EventRegistrations (EventId) INCLUDE (Status, GuestCount);
END
GO

IF COL_LENGTH(N'dbo.EventRegistrations', N'AttendanceStatus') IS NULL
    ALTER TABLE dbo.EventRegistrations ADD AttendanceStatus nvarchar(20) NOT NULL CONSTRAINT DF_EventRegistrations_AttendanceStatus DEFAULT (N'Pending');
GO

PRINT N'--- Events and EventRegistrations tables created ---';