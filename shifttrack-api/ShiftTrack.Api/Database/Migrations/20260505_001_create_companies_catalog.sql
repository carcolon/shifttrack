IF OBJECT_ID(N'dbo.Companies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Companies
    (
        Name NVARCHAR(200) NOT NULL CONSTRAINT PK_Companies PRIMARY KEY,
        IsActive BIT NOT NULL CONSTRAINT DF_Companies_IsActive DEFAULT 1,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Companies_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2 NULL
    );
END;

INSERT INTO dbo.Companies (Name, IsActive)
SELECT DISTINCT LTRIM(RTRIM(u.Company)), 1
FROM dbo.Users u
WHERE u.Company IS NOT NULL
  AND LTRIM(RTRIM(u.Company)) <> N''
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.Companies c
      WHERE c.Name = LTRIM(RTRIM(u.Company))
  );

IF NOT EXISTS (SELECT 1 FROM dbo.Companies WHERE Name = N'Esquire Law, LLC')
BEGIN
    INSERT INTO dbo.Companies (Name, IsActive)
    VALUES (N'Esquire Law, LLC', 1);
END;
