IF OBJECT_ID(N'dbo.CompanyOperations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyOperations
    (
        CompanyName NVARCHAR(200) NOT NULL,
        Name NVARCHAR(120) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CompanyOperations_IsActive DEFAULT 1,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CompanyOperations_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2 NULL,
        CONSTRAINT PK_CompanyOperations PRIMARY KEY (CompanyName, Name)
    );
END;

MERGE dbo.CompanyOperations AS target
USING (VALUES
    (N'Esquire Law, LLC', N'ESQ'),
    (N'Esquire Law, LLC', N'Leaders'),
    (N'Esquire Law, LLC', N'Outbound'),
    (N'Esquire Law, LLC', N'Referral'),
    (N'Esquire Law, LLC', N'SGF')
) AS source (CompanyName, Name)
ON target.CompanyName = source.CompanyName AND target.Name = source.Name
WHEN NOT MATCHED THEN
    INSERT (CompanyName, Name, IsActive)
    VALUES (source.CompanyName, source.Name, 1);

INSERT INTO dbo.CompanyOperations (CompanyName, Name, IsActive)
SELECT DISTINCT LTRIM(RTRIM(u.Company)), LTRIM(RTRIM(u.Operation)), 1
FROM dbo.Users u
WHERE u.Company IS NOT NULL
  AND LTRIM(RTRIM(u.Company)) <> N''
  AND u.Operation IS NOT NULL
  AND LTRIM(RTRIM(u.Operation)) <> N''
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.CompanyOperations co
      WHERE co.CompanyName = LTRIM(RTRIM(u.Company))
        AND co.Name = LTRIM(RTRIM(u.Operation))
  );
