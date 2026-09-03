IF OBJECT_ID(N'dbo.ScheduleEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduleEvents
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ScheduleEvents PRIMARY KEY,
        EmployeeId UNIQUEIDENTIFIER NULL,
        EmployeeEmail NVARCHAR(320) NOT NULL,
        Action NVARCHAR(50) NOT NULL,
        UpdatedByUserId UNIQUEIDENTIFIER NULL,
        UpdatedByEmail NVARCHAR(320) NOT NULL,
        UpdatedByName NVARCHAR(200) NOT NULL,
        UpdatedByRole INT NOT NULL,
        OccurredAtUtc DATETIME2 NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL
    );

    CREATE INDEX IX_ScheduleEvents_OccurredAtUtc
        ON dbo.ScheduleEvents(OccurredAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.WeeklyCoverageSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WeeklyCoverageSnapshots
    (
        WeekStartDate DATE NOT NULL CONSTRAINT PK_WeeklyCoverageSnapshots PRIMARY KEY,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        ItemsJson NVARCHAR(MAX) NULL,
        CreatedAtUtc DATETIME2 NOT NULL
    );
END;

IF COL_LENGTH('dbo.WeeklyCoverageSnapshots', 'ItemsJson') IS NULL
BEGIN
    ALTER TABLE dbo.WeeklyCoverageSnapshots
    ADD ItemsJson NVARCHAR(MAX) NULL;
END;

IF OBJECT_ID(N'dbo.UserScheduleOverrides', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserScheduleOverrides
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserScheduleOverrides PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        OverrideDate DATE NOT NULL,
        GroupId UNIQUEIDENTIFIER NULL,
        EntryType NVARCHAR(40) NOT NULL,
        RequestType NVARCHAR(40) NULL,
        Comments NVARCHAR(1000) NULL,
        StartTime NVARCHAR(8) NULL,
        EndTime NVARCHAR(8) NULL,
        Label NVARCHAR(120) NULL,
        CreatedAtUtc DATETIME2 NOT NULL
    );

    CREATE UNIQUE INDEX UX_UserScheduleOverrides_UserDate
        ON dbo.UserScheduleOverrides(UserId, OverrideDate);

    CREATE INDEX IX_UserScheduleOverrides_OverrideDate
        ON dbo.UserScheduleOverrides(OverrideDate);
END;

IF COL_LENGTH('dbo.UserScheduleOverrides', 'GroupId') IS NULL
BEGIN
    ALTER TABLE dbo.UserScheduleOverrides
    ADD GroupId UNIQUEIDENTIFIER NULL;
END;

IF COL_LENGTH('dbo.UserScheduleOverrides', 'RequestType') IS NULL
BEGIN
    ALTER TABLE dbo.UserScheduleOverrides
    ADD RequestType NVARCHAR(40) NULL;
END;

IF COL_LENGTH('dbo.UserScheduleOverrides', 'Comments') IS NULL
BEGIN
    ALTER TABLE dbo.UserScheduleOverrides
    ADD Comments NVARCHAR(1000) NULL;
END;

IF OBJECT_ID(N'dbo.PtoRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PtoRequests
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PtoRequests PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        UserEmail NVARCHAR(320) NOT NULL,
        UserDisplayName NVARCHAR(200) NOT NULL,
        RequestType NVARCHAR(40) NOT NULL,
        NumberOfDays INT NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        Comments NVARCHAR(1000) NULL,
        OverrideGroupId UNIQUEIDENTIFIER NULL,
        Status NVARCHAR(20) NOT NULL,
        RequestedByEmail NVARCHAR(320) NOT NULL,
        RequestedByName NVARCHAR(200) NOT NULL,
        RequestedByRole INT NOT NULL,
        ReviewedByEmail NVARCHAR(320) NULL,
        ReviewedByName NVARCHAR(200) NULL,
        ReviewedByRole INT NULL,
        ReviewedAtUtc DATETIME2 NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL
    );

    CREATE INDEX IX_PtoRequests_UserId
        ON dbo.PtoRequests(UserId);

    CREATE INDEX IX_PtoRequests_Status
        ON dbo.PtoRequests(Status);

    CREATE INDEX IX_PtoRequests_StartDate
        ON dbo.PtoRequests(StartDate);
END;

IF COL_LENGTH('dbo.PtoRequests', 'OverrideGroupId') IS NULL
BEGIN
    ALTER TABLE dbo.PtoRequests
    ADD OverrideGroupId UNIQUEIDENTIFIER NULL;
END;
