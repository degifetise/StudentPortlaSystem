USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* Idempotent upgrade from weighted letter grades to four numerical components. */
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = N'PARENT')
BEGIN
    INSERT INTO dbo.AspNetRoles (Id, [Name], NormalizedName, ConcurrencyStamp)
    VALUES (LOWER(CONVERT(nvarchar(36), NEWID())), N'Parent', N'PARENT', LOWER(CONVERT(nvarchar(36), NEWID())));
END
GO

UPDATE dbo.AssessmentTypes
SET WeightPercentage = CASE [Name]
        WHEN N'Quiz' THEN 10.00
        WHEN N'Test' THEN 10.00
        WHEN N'MidExam' THEN 30.00
        WHEN N'FinalExam' THEN 50.00
        WHEN N'Assignment' THEN 0.01
        ELSE WeightPercentage
    END,
    DisplayOrder = CASE [Name]
        WHEN N'Quiz' THEN 1
        WHEN N'Test' THEN 2
        WHEN N'MidExam' THEN 3
        WHEN N'FinalExam' THEN 4
        WHEN N'Assignment' THEN 99
        ELSE DisplayOrder
    END,
    IsActive = CASE WHEN [Name] = N'Assignment' THEN 0 ELSE 1 END;
GO

CREATE OR ALTER VIEW dbo.vw_StudentSubjectPerformance
AS
WITH TypeAgg AS
(
    SELECT m.StudentId, m.SubjectId, a.AssessmentType,
           SUM(m.Score) AS Earned, SUM(a.MaxScore) AS Possible
    FROM dbo.Marks AS m
    INNER JOIN dbo.Assessments AS a ON a.Id = m.AssessmentId
    WHERE m.IsPublished = 1
    GROUP BY m.StudentId, m.SubjectId, a.AssessmentType
), Weighted AS
(
    SELECT t.StudentId, t.SubjectId, t.AssessmentType,
           CAST(t.Earned / NULLIF(t.Possible, 0) * atw.WeightPercentage AS decimal(6,2)) AS WeightedScore
    FROM TypeAgg AS t
    INNER JOIN dbo.AssessmentTypes AS atw ON atw.[Name] = t.AssessmentType
    WHERE atw.IsActive = 1
)
SELECT w.StudentId, w.SubjectId,
       SUM(CASE WHEN w.AssessmentType = N'Quiz' THEN w.WeightedScore END) AS QuizScore,
       SUM(CASE WHEN w.AssessmentType = N'Test' THEN w.WeightedScore END) AS TestScore,
       SUM(CASE WHEN w.AssessmentType = N'MidExam' THEN w.WeightedScore END) AS MidExamScore,
       SUM(CASE WHEN w.AssessmentType = N'FinalExam' THEN w.WeightedScore END) AS FinalExamScore,
       CAST(SUM(w.WeightedScore) AS decimal(6,2)) AS TotalScore,
       CAST(CASE WHEN SUM(w.WeightedScore) >= 50 THEN 1 ELSE 0 END AS bit) AS IsPassed,
       CASE WHEN SUM(w.WeightedScore) >= 50 THEN N'Pass' ELSE N'Fail' END AS [Status]
FROM Weighted AS w
GROUP BY w.StudentId, w.SubjectId;
GO
