MERGE dbo.Holidays AS target
USING
(
    VALUES
        (CAST('2025-01-01' AS date), N'Año Nuevo', N'CO'),
        (CAST('2025-01-06' AS date), N'Epifanía', N'CO'),
        (CAST('2025-03-24' AS date), N'Día de San José', N'CO'),
        (CAST('2025-04-17' AS date), N'Jueves Santo', N'CO'),
        (CAST('2025-04-18' AS date), N'Viernes Santo', N'CO'),
        (CAST('2025-05-01' AS date), N'Día del trabajo', N'CO'),
        (CAST('2025-06-02' AS date), N'Ascensión de Jesús', N'CO'),
        (CAST('2025-06-23' AS date), N'Corpus Christi', N'CO'),
        (CAST('2025-06-30' AS date), N'Sagrado Corazón de Jesús / San Pedro y San Pablo', N'CO'),
        (CAST('2025-07-20' AS date), N'Día de la Independencia', N'CO'),
        (CAST('2025-08-07' AS date), N'Batalla de Boyacá', N'CO'),
        (CAST('2025-08-18' AS date), N'Asunción de la Virgen', N'CO'),
        (CAST('2025-10-13' AS date), N'Día de la Diversidad Étnica y Cultural', N'CO'),
        (CAST('2025-11-03' AS date), N'Todos los Santos', N'CO'),
        (CAST('2025-11-17' AS date), N'Independencia de Cartagena', N'CO'),
        (CAST('2025-12-08' AS date), N'Inmaculada Concepción', N'CO'),
        (CAST('2025-12-25' AS date), N'Navidad', N'CO'),

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
        (CAST('2027-12-25' AS date), N'Navidad', N'CO'),

        (CAST('2028-01-01' AS date), N'Año Nuevo', N'CO'),
        (CAST('2028-01-10' AS date), N'Reyes Magos', N'CO'),
        (CAST('2028-03-20' AS date), N'Día de San José', N'CO'),
        (CAST('2028-04-13' AS date), N'Jueves Santo', N'CO'),
        (CAST('2028-04-14' AS date), N'Viernes Santo', N'CO'),
        (CAST('2028-05-01' AS date), N'Día del trabajo', N'CO'),
        (CAST('2028-05-29' AS date), N'Ascensión de Jesús', N'CO'),
        (CAST('2028-06-19' AS date), N'Corpus Christi', N'CO'),
        (CAST('2028-06-26' AS date), N'Sagrado Corazón de Jesús', N'CO'),
        (CAST('2028-07-03' AS date), N'San Pedro y San Pablo', N'CO'),
        (CAST('2028-07-20' AS date), N'Día de la Independencia', N'CO'),
        (CAST('2028-08-07' AS date), N'Batalla de Boyacá', N'CO'),
        (CAST('2028-08-21' AS date), N'Asunción de la Virgen', N'CO'),
        (CAST('2028-10-16' AS date), N'Día de la raza', N'CO'),
        (CAST('2028-11-06' AS date), N'Todos los Santos', N'CO'),
        (CAST('2028-11-13' AS date), N'Independencia de Cartagena', N'CO'),
        (CAST('2028-12-08' AS date), N'Inmaculada Concepción', N'CO'),
        (CAST('2028-12-25' AS date), N'Navidad', N'CO')
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
