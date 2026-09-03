IF COL_LENGTH('dbo.Users', 'CompanyScope') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD CompanyScope NVARCHAR(MAX) NULL;
END;

EXEC(N'
UPDATE dbo.Users
SET CompanyScope = N''["'' + REPLACE(REPLACE(Company, N''\'', N''\\''), N''"'', N''\"'') + N''"]''
WHERE (CompanyScope IS NULL OR LTRIM(RTRIM(CompanyScope)) = N'''')
  AND Company IS NOT NULL
  AND LTRIM(RTRIM(Company)) <> N'''';
');
