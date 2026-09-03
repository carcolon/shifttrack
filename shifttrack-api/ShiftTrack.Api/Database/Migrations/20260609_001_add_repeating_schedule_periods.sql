IF COL_LENGTH('dbo.UserSchedulePeriods', 'IsRepeating') IS NULL
BEGIN
    ALTER TABLE dbo.UserSchedulePeriods
        ADD IsRepeating BIT NOT NULL CONSTRAINT DF_UserSchedulePeriods_IsRepeating DEFAULT 0;
END;
