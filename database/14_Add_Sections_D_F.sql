/* Add the remaining standard sections to an existing installation without
   changing any sections an administrator has already renamed or re-coded. */
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

INSERT INTO dbo.Sections ([Name], Code, Capacity)
SELECT source.[Name], source.Code, 40
FROM (VALUES
    (N'Section D', N'D'),
    (N'Section E', N'E'),
    (N'Section F', N'F')
) AS source ([Name], Code)
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.Sections AS existing
    WHERE existing.Code = source.Code
       OR existing.[Name] = source.[Name]
);

COMMIT TRANSACTION;
GO
