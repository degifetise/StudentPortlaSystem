USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.StudentFeedbacks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentFeedbacks
    (
        Id          bigint IDENTITY(1,1) NOT NULL,
        StudentId   int NOT NULL,
        Category    nvarchar(30) NOT NULL,
        Message     nvarchar(4000) NOT NULL,
        Status      nvarchar(30) NOT NULL CONSTRAINT DF_StudentFeedbacks_Status DEFAULT (N'Pending Review'),
        SubmittedAt datetime2(7) NOT NULL CONSTRAINT DF_StudentFeedbacks_SubmittedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt   datetime2(7) NULL,
        CONSTRAINT PK_StudentFeedbacks PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_StudentFeedbacks_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT CK_StudentFeedbacks_Category CHECK (Category IN (N'Subject', N'Teacher', N'Marks')),
        CONSTRAINT CK_StudentFeedbacks_Status CHECK (Status IN (N'Pending Review', N'Acknowledged', N'Resolved'))
    );

    CREATE NONCLUSTERED INDEX IX_StudentFeedbacks_Status_SubmittedAt
        ON dbo.StudentFeedbacks (Status, SubmittedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.EventComments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventComments
    (
        Id        bigint IDENTITY(1,1) NOT NULL,
        EventId   int NOT NULL,
        StudentId int NOT NULL,
        Message   nvarchar(2000) NOT NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EventComments_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EventComments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_EventComments_Events_EventId FOREIGN KEY (EventId)
            REFERENCES dbo.Events (Id) ON DELETE CASCADE,
        CONSTRAINT FK_EventComments_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_EventComments_Event_CreatedAt
        ON dbo.EventComments (EventId, CreatedAt);
END
GO

PRINT N'--- Student feedback and event comments tables are ready ---';
