IF OBJECT_ID(N'dbo.PasswordResetRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetRequests
    (
        Id bigint IDENTITY(1,1) NOT NULL,
        UserId nvarchar(450) NOT NULL,
        Token nvarchar(256) NOT NULL,
        ExpiresAt datetime2 NOT NULL,
        UsedAt datetime2 NULL,
        RequestedFromIp nvarchar(45) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_PasswordResetRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PasswordResetRequests PRIMARY KEY (Id),
        CONSTRAINT FK_PasswordResetRequests_AspNetUsers FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_PasswordResetRequests_Token' AND object_id = OBJECT_ID(N'dbo.PasswordResetRequests'))
BEGIN
    CREATE UNIQUE INDEX UQ_PasswordResetRequests_Token
        ON dbo.PasswordResetRequests(Token);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PasswordResetRequests_UserId_ExpiresAt' AND object_id = OBJECT_ID(N'dbo.PasswordResetRequests'))
BEGIN
    CREATE INDEX IX_PasswordResetRequests_UserId_ExpiresAt
        ON dbo.PasswordResetRequests(UserId, ExpiresAt);
END;
