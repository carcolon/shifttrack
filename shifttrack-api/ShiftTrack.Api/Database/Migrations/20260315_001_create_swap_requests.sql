IF OBJECT_ID(N'dbo.SwapRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SwapRequests
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SwapRequests PRIMARY KEY,
        RequestedByUserId UNIQUEIDENTIFIER NOT NULL,
        RequestedByEmail NVARCHAR(320) NOT NULL,
        RequestedByDisplayName NVARCHAR(200) NOT NULL,
        RequestedByRole INT NOT NULL,
        TargetUserId UNIQUEIDENTIFIER NOT NULL,
        TargetUserEmail NVARCHAR(320) NOT NULL,
        TargetUserDisplayName NVARCHAR(200) NOT NULL,
        TargetUserRole INT NOT NULL,
        SwapDate DATE NOT NULL,
        RequestType NVARCHAR(40) NOT NULL,
        Comments NVARCHAR(1000) NULL,
        Status NVARCHAR(20) NOT NULL,
        ReviewedByEmail NVARCHAR(320) NULL,
        ReviewedByName NVARCHAR(200) NULL,
        ReviewedByRole INT NULL,
        ReviewedAtUtc DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SwapRequests_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SwapRequests_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_SwapRequests_Status CHECK (Status IN ('pending', 'approved', 'denied'))
    );

    CREATE INDEX IX_SwapRequests_Status_CreatedAtUtc
        ON dbo.SwapRequests(Status, CreatedAtUtc DESC);

    CREATE INDEX IX_SwapRequests_TargetUserId_Status
        ON dbo.SwapRequests(TargetUserId, Status, CreatedAtUtc DESC);

    CREATE INDEX IX_SwapRequests_RequestedByRole_Status
        ON dbo.SwapRequests(RequestedByRole, Status, CreatedAtUtc DESC);
END;
