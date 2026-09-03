IF COL_LENGTH('dbo.Users', 'IsSystemHidden') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD IsSystemHidden BIT NOT NULL CONSTRAINT DF_Users_IsSystemHidden DEFAULT 0;
END;
