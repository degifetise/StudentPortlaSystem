USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.Events', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.Events
        ADD IsDeleted bit NOT NULL
            CONSTRAINT DF_Events_IsDeleted DEFAULT (0);
END
GO

-- Older versions represented a deleted event as unpublished and cancelled without
-- a status override. Preserve ordinary cancellations while hiding those old deletes.
UPDATE dbo.Events
SET IsDeleted = 1
WHERE IsDeleted = 0
  AND IsCancelled = 1
  AND IsPublished = 0
  AND StatusOverride IS NULL;
GO

PRINT N'--- Events now support persistent soft deletion ---';
