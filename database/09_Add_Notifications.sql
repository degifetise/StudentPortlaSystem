USE [HaladeHighSchoolDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications
    (
        Id        bigint IDENTITY(1,1) NOT NULL,
        UserId    nvarchar(450) NOT NULL,
        Title     nvarchar(200) NOT NULL,
        Message   nvarchar(1000) NOT NULL,
        Type      nvarchar(30) NOT NULL,
        TargetUrl nvarchar(500) NOT NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ReadAt    datetime2(7) NULL,
        CONSTRAINT PK_Notifications PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Notifications_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_Notifications_User_Read_Created
        ON dbo.Notifications (UserId, ReadAt, CreatedAt DESC);
END
GO

PRINT N'--- User notifications table created ---';