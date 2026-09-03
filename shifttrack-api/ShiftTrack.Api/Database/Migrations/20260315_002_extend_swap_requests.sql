IF COL_LENGTH('dbo.SwapRequests', 'RequestedDatesJson') IS NULL
BEGIN
    ALTER TABLE dbo.SwapRequests
    ADD RequestedDatesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SwapRequests_RequestedDatesJson DEFAULT N'[]';
END;

IF COL_LENGTH('dbo.SwapRequests', 'TargetDatesJson') IS NULL
BEGIN
    ALTER TABLE dbo.SwapRequests
    ADD TargetDatesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SwapRequests_TargetDatesJson DEFAULT N'[]';
END;

IF COL_LENGTH('dbo.SwapRequests', 'PairingsJson') IS NULL
BEGIN
    ALTER TABLE dbo.SwapRequests
    ADD PairingsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SwapRequests_PairingsJson DEFAULT N'[]';
END;

IF COL_LENGTH('dbo.SwapRequests', 'AppliedGroupId') IS NULL
BEGIN
    ALTER TABLE dbo.SwapRequests
    ADD AppliedGroupId UNIQUEIDENTIFIER NULL;
END;
