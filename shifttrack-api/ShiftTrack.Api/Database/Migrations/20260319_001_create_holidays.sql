IF OBJECT_ID(N'dbo.Holidays', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Holidays
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Holidays PRIMARY KEY,
        [Date] DATE NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        CountryCode NVARCHAR(10) NOT NULL CONSTRAINT DF_Holidays_CountryCode DEFAULT N'CO',
        IsActive BIT NOT NULL CONSTRAINT DF_Holidays_IsActive DEFAULT 1,
        IsManual BIT NOT NULL CONSTRAINT DF_Holidays_IsManual DEFAULT 0,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Holidays_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Holidays_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_Holidays_Date_CountryCode
        ON dbo.Holidays([Date], CountryCode);
END;

MERGE dbo.Holidays AS target
USING
(
    VALUES
        (CAST('2026-01-01' AS date), N'Año Nuevo', N'CO'),
        (CAST('2026-01-12' AS date), N'Epifanía', N'CO'),
        (CAST('2026-03-23' AS date), N'Día de San José', N'CO'),
        (CAST('2026-04-02' AS date), N'Jueves Santo', N'CO'),
        (CAST('2026-04-03' AS date), N'Viernes Santo', N'CO'),
        (CAST('2026-05-01' AS date), N'Día del trabajo', N'CO'),
        (CAST('2026-05-18' AS date), N'Ascensión de Jesús', N'CO'),
        (CAST('2026-06-08' AS date), N'Corpus Christi', N'CO'),
        (CAST('2026-06-15' AS date), N'Sagrado Corazón de Jesús', N'CO'),
        (CAST('2026-06-29' AS date), N'San Pedro y San Pablo', N'CO'),
        (CAST('2026-07-20' AS date), N'Día de la Independencia', N'CO'),
        (CAST('2026-08-07' AS date), N'Batalla de Boyacá', N'CO'),
        (CAST('2026-08-17' AS date), N'Asunción de la Virgen', N'CO'),
        (CAST('2026-10-12' AS date), N'Día de la Diversidad Étnica y Cultural', N'CO'),
        (CAST('2026-11-02' AS date), N'Todos los Santos', N'CO'),
        (CAST('2026-11-16' AS date), N'Independencia de Cartagena', N'CO'),
        (CAST('2026-12-08' AS date), N'Inmaculada Concepción', N'CO'),
        (CAST('2026-12-25' AS date), N'Navidad', N'CO'),

        (CAST('2027-01-01' AS date), N'Año Nuevo', N'CO'),
        (CAST('2027-01-11' AS date), N'Epifanía', N'CO'),
        (CAST('2027-03-22' AS date), N'Día de San José', N'CO'),
        (CAST('2027-03-25' AS date), N'Jueves Santo', N'CO'),
        (CAST('2027-03-26' AS date), N'Viernes Santo', N'CO'),
        (CAST('2027-05-01' AS date), N'Día del trabajo', N'CO'),
        (CAST('2027-05-10' AS date), N'Ascensión de Jesús', N'CO'),
        (CAST('2027-05-31' AS date), N'Corpus Christi', N'CO'),
        (CAST('2027-06-07' AS date), N'Sagrado Corazón de Jesús', N'CO'),
        (CAST('2027-07-05' AS date), N'San Pedro y San Pablo', N'CO'),
        (CAST('2027-07-20' AS date), N'Día de la Independencia', N'CO'),
        (CAST('2027-08-07' AS date), N'Batalla de Boyacá', N'CO'),
        (CAST('2027-08-16' AS date), N'Asunción de la Virgen', N'CO'),
        (CAST('2027-10-18' AS date), N'Día de la Diversidad Étnica y Cultural', N'CO'),
        (CAST('2027-11-01' AS date), N'Todos los Santos', N'CO'),
        (CAST('2027-11-15' AS date), N'Independencia de Cartagena', N'CO'),
        (CAST('2027-12-08' AS date), N'Inmaculada Concepción', N'CO'),
        (CAST('2027-12-25' AS date), N'Navidad', N'CO')
) AS source([Date], [Name], CountryCode)
ON target.[Date] = source.[Date]
   AND target.CountryCode = source.CountryCode
WHEN MATCHED THEN
    UPDATE SET
        [Name] = source.[Name],
        IsActive = 1,
        UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, [Date], [Name], CountryCode, IsActive, IsManual, CreatedAtUtc, UpdatedAtUtc)
    VALUES (NEWID(), source.[Date], source.[Name], source.CountryCode, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
