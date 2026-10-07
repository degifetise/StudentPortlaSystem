USE [HaladeHighSchoolDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.GradeLevels')
      AND name = N'CK_GradeLevels_Level'
)
BEGIN
    ALTER TABLE dbo.GradeLevels DROP CONSTRAINT CK_GradeLevels_Level;
END

ALTER TABLE dbo.GradeLevels
    ADD CONSTRAINT CK_GradeLevels_Level CHECK ([Level] BETWEEN -3 AND 12);

INSERT INTO dbo.GradeLevels ([Name], [Level], [Description])
SELECT src.[Name], src.[Level], src.[Description]
FROM (VALUES
    (N'Nursery', -3, N'Early childhood'),
    (N'KG',      -2, N'Kindergarten'),
    (N'LKG',     -1, N'Lower kindergarten'),
    (N'UKG',      0, N'Upper kindergarten'),
    (N'Grade 1',  1, N'Primary school'),
    (N'Grade 2',  2, N'Primary school'),
    (N'Grade 3',  3, N'Primary school'),
    (N'Grade 4',  4, N'Primary school'),
    (N'Grade 5',  5, N'Primary school'),
    (N'Grade 6',  6, N'Middle school'),
    (N'Grade 7',  7, N'Middle school'),
    (N'Grade 8',  8, N'Middle school'),
    (N'Grade 9',  9, N'High school'),
    (N'Grade 10', 10, N'High school'),
    (N'Grade 11', 11, N'Preparatory school'),
    (N'Grade 12', 12, N'Preparatory school')
) AS src ([Name], [Level], [Description])
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.GradeLevels AS existing WHERE existing.[Level] = src.[Level]
);

INSERT INTO dbo.Subjects (SubjectName, Code, GradeLevelId, [Description], CreditHours)
SELECT
    catalogue.SubjectName,
    catalogue.Prefix + N'-' + CAST(grade.[Level] AS nvarchar(2)),
    grade.Id,
    catalogue.SubjectName + N' for ' + grade.[Name],
    catalogue.CreditHours
FROM dbo.GradeLevels AS grade
CROSS JOIN (VALUES
    (N'English',                 N'ENG',  4),
    (N'Mathematics',             N'MATH', 5),
    (N'Physics',                 N'PHY',  4),
    (N'Chemistry',               N'CHEM', 4),
    (N'Biology',                 N'BIO',  4),
    (N'History',                 N'HIST', 3),
    (N'Geography',               N'GEO',  3),
    (N'Civics',                  N'CIV',  2),
    (N'Information Technology', N'ICT',  3),
    (N'Physical Education',      N'PE',   2)
) AS catalogue (SubjectName, Prefix, CreditHours)
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.Subjects AS existing
    WHERE existing.GradeLevelId = grade.Id
      AND existing.SubjectName = catalogue.SubjectName
);

UPDATE users
SET Email = LOWER(LEFT(users.Email, CHARINDEX('@', users.Email) - 1) + N'@sms.edu'),
    NormalizedEmail = UPPER(LEFT(users.Email, CHARINDEX('@', users.Email) - 1) + N'@sms.edu'),
    UserName = CASE
        WHEN users.UserName LIKE N'%@%halade%'
            THEN LEFT(users.UserName, CHARINDEX('@', users.UserName) - 1) + N'@sms.edu'
        ELSE users.UserName
    END,
    NormalizedUserName = CASE
        WHEN users.UserName LIKE N'%@%halade%'
            THEN UPPER(LEFT(users.UserName, CHARINDEX('@', users.UserName) - 1) + N'@sms.edu')
        ELSE users.NormalizedUserName
    END
FROM dbo.AspNetUsers AS users
WHERE users.Email LIKE N'%@%halade%';

UPDATE dbo.SystemSettings
SET [Value] = N'School Management System'
WHERE [Key] = N'SchoolName';

UPDATE dbo.SystemSettings
SET [Value] = N'info@sms.edu'
WHERE [Key] = N'ContactEmail';

UPDATE dbo.Announcements
SET Title = N'Welcome to the School Management System'
WHERE Title LIKE N'Welcome to the %';

COMMIT TRANSACTION;
GO
