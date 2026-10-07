USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.Events', N'TargetGradeIdsCsv') IS NULL
    ALTER TABLE dbo.Events ADD TargetGradeIdsCsv nvarchar(1000) NULL;
GO

IF COL_LENGTH(N'dbo.Events', N'TargetSectionIdsCsv') IS NULL
    ALTER TABLE dbo.Events ADD TargetSectionIdsCsv nvarchar(1000) NULL;
GO

UPDATE dbo.Events
SET TargetGradeIdsCsv = N'|' + CONVERT(nvarchar(20), GradeLevelId) + N'|'
WHERE GradeLevelId IS NOT NULL
    AND (TargetGradeIdsCsv IS NULL OR TargetGradeIdsCsv = N'');

UPDATE dbo.Events
SET TargetSectionIdsCsv = N'|' + CONVERT(nvarchar(20), SectionId) + N'|'
WHERE SectionId IS NOT NULL
    AND (TargetSectionIdsCsv IS NULL OR TargetSectionIdsCsv = N'');
GO

PRINT N'--- Event grade and section target scopes added ---';