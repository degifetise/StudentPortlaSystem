/* Smart ID cards, profile attributes and verification scan audit. Safe to re-run. */
USE [HaladeHighSchoolDb];
GO

IF COL_LENGTH(N'dbo.AspNetUsers', N'PhotoUrl') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD PhotoUrl nvarchar(500) NULL;
GO

UPDATE dbo.AspNetUsers SET PhotoUrl = ProfileImageUrl
WHERE PhotoUrl IS NULL AND ProfileImageUrl IS NOT NULL;
GO

IF COL_LENGTH(N'dbo.AspNetUsers', N'DigitalSignatureUrl') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD DigitalSignatureUrl nvarchar(500) NULL;
GO

IF COL_LENGTH(N'dbo.Students', N'EmergencyContact') IS NULL
    ALTER TABLE dbo.Students ADD EmergencyContact nvarchar(100) NULL;
GO

IF COL_LENGTH(N'dbo.Students', N'BloodGroup') IS NULL
    ALTER TABLE dbo.Students ADD BloodGroup nvarchar(10) NULL;
GO

IF OBJECT_ID(N'dbo.SmartCardSerialSequence', N'SO') IS NULL
    EXEC(N'CREATE SEQUENCE dbo.SmartCardSerialSequence AS bigint START WITH 1 INCREMENT BY 1;');
GO

CREATE OR ALTER PROCEDURE dbo.GenerateSmartCardSerial
    @CardUID nvarchar(100) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Serial bigint = NEXT VALUE FOR dbo.SmartCardSerialSequence;
    SET @CardUID = CONCAT(N'SID-', YEAR(SYSUTCDATETIME()), N'-', RIGHT(CONCAT(N'00000000', @Serial), 8));
END;
GO

IF OBJECT_ID(N'dbo.SmartCards', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmartCards
    (
        CardId         uniqueidentifier NOT NULL CONSTRAINT DF_SmartCards_CardId DEFAULT (NEWID()),
        UserId         nvarchar(450) NOT NULL,
        CardUID        nvarchar(100) NOT NULL,
        QrTokenHash    nvarchar(512) NOT NULL,
        CardType       nvarchar(20) NOT NULL,
        [Status]       nvarchar(20) NOT NULL CONSTRAINT DF_SmartCards_Status DEFAULT (N'ACTIVE'),
        IssuedDate     datetime2(7) NOT NULL CONSTRAINT DF_SmartCards_IssuedDate DEFAULT (SYSUTCDATETIME()),
        ExpirationDate datetime2(7) NOT NULL,
        CreatedAt      datetime2(7) NOT NULL CONSTRAINT DF_SmartCards_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SmartCards PRIMARY KEY CLUSTERED (CardId),
        CONSTRAINT UQ_SmartCards_UserId UNIQUE NONCLUSTERED (UserId),
        CONSTRAINT UQ_SmartCards_CardUID UNIQUE NONCLUSTERED (CardUID),
        CONSTRAINT FK_SmartCards_AspNetUsers_UserId FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
        CONSTRAINT CK_SmartCards_CardType CHECK (CardType IN (N'STUDENT', N'STAFF', N'TEACHER')),
        CONSTRAINT CK_SmartCards_Status CHECK ([Status] IN (N'ACTIVE', N'SUSPENDED', N'LOST', N'EXPIRED'))
    );
END
GO

IF OBJECT_ID(N'dbo.SmartIDScanLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmartIDScanLogs
    (
        Id             bigint IDENTITY(1,1) NOT NULL,
        ScannerUserId  nvarchar(450) NOT NULL,
        ScannedUserId  nvarchar(450) NULL,
        ScanLocation   nvarchar(200) NULL,
        ScanTimestamp  datetime2(7) NOT NULL CONSTRAINT DF_SmartIDScanLogs_ScanTimestamp DEFAULT (SYSUTCDATETIME()),
        ScanStatus     nvarchar(20) NOT NULL,
        CONSTRAINT PK_SmartIDScanLogs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_SmartIDScanLogs_ScanStatus CHECK (ScanStatus IN (N'VALID', N'EXPIRED', N'SUSPENDED', N'LOST', N'INVALID', N'NOT_FOUND'))
    );

    CREATE NONCLUSTERED INDEX IX_SmartIDScanLogs_ScanTimestamp
        ON dbo.SmartIDScanLogs (ScanTimestamp DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SmartIDScanLogs_ScanTimestamp'
               AND object_id = OBJECT_ID(N'dbo.SmartIDScanLogs'))
    CREATE NONCLUSTERED INDEX IX_SmartIDScanLogs_ScanTimestamp
        ON dbo.SmartIDScanLogs (ScanTimestamp DESC);
GO
