/* Upgrade an existing database for student and teacher public applications. */
USE [HaladeHighSchoolDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.StudentRegistrationRequests', N'RequestedRole') IS NULL
BEGIN
    ALTER TABLE dbo.StudentRegistrationRequests
        ADD RequestedRole nvarchar(20) NULL
            CONSTRAINT DF_StudentRegistrationRequests_RequestedRole DEFAULT (N'Student');
END
GO

UPDATE dbo.StudentRegistrationRequests
SET RequestedRole = N'Student'
WHERE RequestedRole IS NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_StudentRegistrationRequests_RequestedRole'
      AND parent_object_id = OBJECT_ID(N'dbo.StudentRegistrationRequests'))
BEGIN
    ALTER TABLE dbo.StudentRegistrationRequests
        ADD CONSTRAINT CK_StudentRegistrationRequests_RequestedRole
            CHECK (RequestedRole IS NULL OR RequestedRole IN (N'Student', N'Teacher'));
END
GO

ALTER TABLE dbo.StudentRegistrationRequests ALTER COLUMN GradeLevelId int NULL;
ALTER TABLE dbo.StudentRegistrationRequests ALTER COLUMN SectionId int NULL;
GO