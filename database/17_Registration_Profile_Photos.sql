/* Persists an optional applicant photo until the account is approved. Safe to re-run. */
USE [HaladeHighSchoolDb];
GO

IF COL_LENGTH(N'dbo.AspNetUsers', N'PhotoUrl') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD PhotoUrl nvarchar(500) NULL;
GO

IF COL_LENGTH(N'dbo.StudentRegistrationRequests', N'PhotoUrl') IS NULL
    ALTER TABLE dbo.StudentRegistrationRequests ADD PhotoUrl nvarchar(500) NULL;
GO
