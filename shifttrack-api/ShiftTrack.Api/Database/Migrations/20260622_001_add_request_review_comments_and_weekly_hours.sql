IF COL_LENGTH('dbo.PtoRequests', 'ReviewComments') IS NULL
BEGIN
    ALTER TABLE dbo.PtoRequests ADD ReviewComments NVARCHAR(1000) NULL;
END;

IF COL_LENGTH('dbo.SwapRequests', 'ReviewComments') IS NULL
BEGIN
    ALTER TABLE dbo.SwapRequests ADD ReviewComments NVARCHAR(1000) NULL;
END;

IF COL_LENGTH('dbo.SwapRequests', 'WeeklyHoursJson') IS NULL
BEGIN
    ALTER TABLE dbo.SwapRequests
    ADD WeeklyHoursJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SwapRequests_WeeklyHoursJson DEFAULT N'[]';
END;
