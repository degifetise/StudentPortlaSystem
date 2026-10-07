USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.EventComments', N'U') IS NULL
    THROW 50001, 'EventComments must be provisioned by database/10_Add_Feedback_And_EventComments.sql first.', 1;
GO

IF COL_LENGTH(N'dbo.EventComments', N'UserId') IS NULL
    ALTER TABLE dbo.EventComments ADD UserId nvarchar(450) NULL;
GO
IF COL_LENGTH(N'dbo.EventComments', N'UserName') IS NULL
    ALTER TABLE dbo.EventComments ADD UserName nvarchar(150) NULL;
GO
IF COL_LENGTH(N'dbo.EventComments', N'UserRole') IS NULL
    ALTER TABLE dbo.EventComments ADD UserRole nvarchar(20) NULL;
GO
IF COL_LENGTH(N'dbo.EventComments', N'ParentCommentId') IS NULL
    ALTER TABLE dbo.EventComments ADD ParentCommentId bigint NULL;
GO
IF COL_LENGTH(N'dbo.EventComments', N'CommentText') IS NULL
    ALTER TABLE dbo.EventComments ADD CommentText nvarchar(2000) NULL;
GO

UPDATE comments
SET CommentText = COALESCE(comments.CommentText, comments.Message),
    UserName = COALESCE(comments.UserName, users.FullName, students.StudentIdNumber, N'Former student'),
    UserRole = COALESCE(comments.UserRole, N'Student'),
    UserId = COALESCE(comments.UserId, students.UserId)
FROM dbo.EventComments AS comments
LEFT JOIN dbo.Students AS students ON students.Id = comments.StudentId
LEFT JOIN dbo.AspNetUsers AS users ON users.Id = students.UserId
WHERE comments.CommentText IS NULL OR comments.UserName IS NULL OR comments.UserRole IS NULL;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.EventComments')
      AND name = N'StudentId'
      AND is_nullable = 0
)
    ALTER TABLE dbo.EventComments ALTER COLUMN StudentId int NULL;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.EventComments')
      AND name = N'Message'
      AND is_nullable = 0
)
    ALTER TABLE dbo.EventComments ALTER COLUMN Message nvarchar(2000) NULL;
GO

ALTER TABLE dbo.EventComments ALTER COLUMN UserName nvarchar(150) NOT NULL;
ALTER TABLE dbo.EventComments ALTER COLUMN UserRole nvarchar(20) NOT NULL;
ALTER TABLE dbo.EventComments ALTER COLUMN CommentText nvarchar(2000) NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_EventComments_AspNetUsers_UserId'
      AND parent_object_id = OBJECT_ID(N'dbo.EventComments')
)
    ALTER TABLE dbo.EventComments
        ADD CONSTRAINT FK_EventComments_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE SET NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_EventComments_EventComments_ParentCommentId'
      AND parent_object_id = OBJECT_ID(N'dbo.EventComments')
)
    ALTER TABLE dbo.EventComments
        ADD CONSTRAINT FK_EventComments_EventComments_ParentCommentId
        FOREIGN KEY (ParentCommentId) REFERENCES dbo.EventComments (Id);
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_EventComments_UserRole'
      AND parent_object_id = OBJECT_ID(N'dbo.EventComments')
)
    ALTER TABLE dbo.EventComments
        ADD CONSTRAINT CK_EventComments_UserRole
        CHECK (UserRole IN (N'Student', N'Teacher', N'Admin'));
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_EventComments_ParentCommentId'
      AND object_id = OBJECT_ID(N'dbo.EventComments')
)
    CREATE NONCLUSTERED INDEX IX_EventComments_ParentCommentId
        ON dbo.EventComments (ParentCommentId);
GO

PRINT N'--- Event comments now support all school roles and administrator replies ---';
