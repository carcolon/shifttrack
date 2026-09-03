IF OBJECT_ID(N'dbo.UserSchedulePeriods', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSchedulePeriods
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserSchedulePeriods PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        EffectiveFrom DATE NOT NULL,
        EffectiveTo DATE NULL,
        ShiftTime NVARCHAR(50) NOT NULL,
        BlocksJson NVARCHAR(MAX) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_UserSchedulePeriods_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_UserSchedulePeriods_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT CK_UserSchedulePeriods_EffectiveRange CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
    );

    CREATE INDEX IX_UserSchedulePeriods_UserId_EffectiveFrom
        ON dbo.UserSchedulePeriods(UserId, EffectiveFrom DESC, EffectiveTo, CreatedAtUtc DESC);
END;

INSERT INTO dbo.UserSchedulePeriods (Id, UserId, EffectiveFrom, EffectiveTo, ShiftTime, BlocksJson, CreatedAtUtc)
SELECT NEWID(),
       u.Id,
       CAST(COALESCE(u.CreatedAtUtc, SYSUTCDATETIME()) AS date),
       NULL,
       COALESCE(NULLIF(LTRIM(RTRIM(u.ShiftTime)), ''), 'Morning'),
       u.ScheduleBlocks,
       SYSUTCDATETIME()
FROM dbo.Users u
WHERE u.ScheduleBlocks IS NOT NULL
  AND LTRIM(RTRIM(u.ScheduleBlocks)) <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.UserSchedulePeriods usp
      WHERE usp.UserId = u.Id
  );
