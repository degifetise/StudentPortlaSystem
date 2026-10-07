/* The Guardian and StudentGuardian tables were introduced in Phase 2. This script
   aligns the link uniqueness key with the current EF Core model and can be rerun. */
USE [HaladeHighSchoolDb];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.UQ_StudentGuardians_Pair', N'UQ') IS NOT NULL
BEGIN
    ALTER TABLE dbo.StudentGuardians
        DROP CONSTRAINT UQ_StudentGuardians_Pair;
END
ELSE IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.StudentGuardians')
      AND name = N'UQ_StudentGuardians_Pair'
)
BEGIN
    DROP INDEX UQ_StudentGuardians_Pair ON dbo.StudentGuardians;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.StudentGuardians')
      AND name = N'UQ_StudentGuardians_Pair'
)
BEGIN
    ALTER TABLE dbo.StudentGuardians
        ADD CONSTRAINT UQ_StudentGuardians_Pair UNIQUE NONCLUSTERED (GuardianId, StudentId);
END

COMMIT TRANSACTION;
GO
