using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ShiftTrack.Infrastructure.Persistence;

public sealed class ShiftTrackSchemaValidator(ShiftTrackDbContext dbContext)
{
    private static readonly string[] RequiredTables =
    [
        "Users",
        "ResetTokens",
        "UserSchedulePeriods",
        "SwapRequests",
        "ScheduleEvents",
        "WeeklyCoverageSnapshots",
        "UserScheduleOverrides",
        "PtoRequests",
        "Holidays",
        "Companies",
        "CoverageRules",
        "CompanyOperations"
    ];

    public async Task ValidateBaselineSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        var existingTables = await GetExistingTablesAsync(cancellationToken);
        var missingTables = RequiredTables
            .Where(table => !existingTables.Contains(table))
            .ToArray();

        if (missingTables.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "The ShiftTrack EF baseline expects an existing database schema. Missing tables: " +
            string.Join(", ", missingTables) +
            ". Restore/apply the historical schema before running EF migrations.");
    }

    private async Task<HashSet<string>> GetExistingTablesAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_TYPE = 'BASE TABLE';
                """;

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
