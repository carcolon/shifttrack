IF OBJECT_ID(N'dbo.CoverageRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoverageRules
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CoverageRules PRIMARY KEY,
        CompanyName NVARCHAR(200) NOT NULL,
        OperationName NVARCHAR(200) NOT NULL CONSTRAINT DF_CoverageRules_OperationName DEFAULT N'',
        DayOfWeek TINYINT NOT NULL,
        ExpectedCoverage INT NOT NULL,
        GreenThreshold INT NOT NULL,
        YellowThreshold INT NOT NULL,
        CalculationScope NVARCHAR(20) NOT NULL CONSTRAINT DF_CoverageRules_CalculationScope DEFAULT N'operation',
        IsActive BIT NOT NULL CONSTRAINT DF_CoverageRules_IsActive DEFAULT 1,
        UpdatedBy NVARCHAR(320) NOT NULL CONSTRAINT DF_CoverageRules_UpdatedBy DEFAULT N'',
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CoverageRules_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_CoverageRules_ScopeDay
        ON dbo.CoverageRules (CompanyName, OperationName, DayOfWeek);
END;

IF OBJECT_ID(N'dbo.CoverageRules', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.CoverageRules', 'CalculationScope') IS NULL
BEGIN
    ALTER TABLE dbo.CoverageRules
    ADD CalculationScope NVARCHAR(20) NOT NULL CONSTRAINT DF_CoverageRules_CalculationScope DEFAULT N'operation';
END;
