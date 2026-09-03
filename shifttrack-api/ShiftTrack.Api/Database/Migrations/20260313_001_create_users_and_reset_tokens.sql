IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ObjectId NVARCHAR(36) NOT NULL,
        Email NVARCHAR(320) NOT NULL,
        DisplayName NVARCHAR(200) NULL,
        Role INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
        IsSystemHidden BIT NOT NULL CONSTRAINT DF_Users_IsSystemHidden DEFAULT 0,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        PasswordHash NVARCHAR(500) NULL,
        MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0,
        Location NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_Location DEFAULT N'',
        Company NVARCHAR(100) NOT NULL CONSTRAINT DF_Users_Company DEFAULT N'',
        Operation NVARCHAR(100) NOT NULL CONSTRAINT DF_Users_Operation DEFAULT N'',
        ShiftTime NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_ShiftTime DEFAULT N'Morning',
        ScheduleBlocks NVARCHAR(MAX) NULL
    );

    CREATE UNIQUE INDEX UX_Users_Email
        ON dbo.Users(Email);

    CREATE INDEX IX_Users_ObjectId
        ON dbo.Users(ObjectId);

    CREATE INDEX IX_Users_IsActive
        ON dbo.Users(IsActive);
END;

IF COL_LENGTH('dbo.Users', 'IsSystemHidden') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD IsSystemHidden BIT NOT NULL CONSTRAINT DF_Users_IsSystemHidden DEFAULT 0;
END;

IF OBJECT_ID(N'dbo.ResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ResetTokens
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ResetTokens PRIMARY KEY,
        Email NVARCHAR(320) NOT NULL,
        TokenHash NVARCHAR(128) NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ResetTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UsedAtUtc DATETIME2 NULL
    );

    CREATE INDEX IX_ResetTokens_Email_UsedAtUtc_ExpiresAt
        ON dbo.ResetTokens(Email, UsedAtUtc, ExpiresAt);
END;
