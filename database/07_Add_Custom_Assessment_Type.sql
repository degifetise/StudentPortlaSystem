USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.Assessments', N'CustomTypeTitle') IS NULL
    ALTER TABLE dbo.Assessments ADD CustomTypeTitle nvarchar(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AssessmentTypes WHERE [Name] = N'Other')
BEGIN
    INSERT INTO dbo.AssessmentTypes ([Name], DisplayName, WeightPercentage, DisplayOrder, IsActive)
    VALUES (N'Other', N'Other', 0.01, 100, 0);
END
GO

PRINT N'--- Custom assessment type support added ---';