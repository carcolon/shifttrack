using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ShiftTrack.Application;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class BulkUserUploadHelpers
{
    internal const int HeaderRow = 10;
    internal const int MaxRows = 1000;
    private const int ExcelMaxBytes = 5 * 1024 * 1024;
    private static readonly string[] ExpectedHeaders =
    [
        "First Name*",
        "Last Name*",
        "Email*",
        "Role*",
        "Location*",
        "Companies*",
        "Primary Company*",
        "Operation*",
        "Period Number*",
        "Effective From*",
        "Effective To",
        "Shift Time*",
        "Is Repeating?",
        "Block Number*",
        "Start*",
        "End*",
        "Days*",
        "Notes"
    ];

    private static readonly Dictionary<string, int> RoleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Employee"] = RoleHelpers.Employee,
        ["Manager"] = RoleHelpers.Manager,
        ["Admin"] = RoleHelpers.Admin,
        ["Team Leader"] = RoleHelpers.TeamLeader,
        ["TeamLeader"] = RoleHelpers.TeamLeader
    };

    private static readonly HashSet<string> ValidDays = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"
    };

    internal static async Task<(IReadOnlyList<BulkUploadRow> Rows, IReadOnlyList<BulkUserUploadError> Errors)> ReadRowsAsync(IFormFile file)
    {
        if (file.Length <= 0)
        {
            return (Array.Empty<BulkUploadRow>(), [new BulkUserUploadError(0, "File", string.Empty, "Upload file is empty.")]);
        }

        if (file.Length > ExcelMaxBytes)
        {
            return (Array.Empty<BulkUploadRow>(), [new BulkUserUploadError(0, "File", string.Empty, "Upload file must be 5 MB or smaller.")]);
        }

        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return (Array.Empty<BulkUploadRow>(), [new BulkUserUploadError(0, "File", string.Empty, "Upload file must be an .xlsx workbook.")]);
        }

        await using var stream = file.OpenReadStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetPath = ResolveFirstWorksheetPath(archive);
        if (sheetPath is null)
        {
            return (Array.Empty<BulkUploadRow>(), [new BulkUserUploadError(0, "Workbook", string.Empty, "Workbook does not contain a readable worksheet.")]);
        }

        var sheetEntry = archive.GetEntry(sheetPath);
        if (sheetEntry is null)
        {
            return (Array.Empty<BulkUploadRow>(), [new BulkUserUploadError(0, "Workbook", string.Empty, "Workbook worksheet relationship is invalid.")]);
        }

        var cells = ReadCells(sheetEntry, sharedStrings);
        var headerErrors = ValidateHeaders(cells);
        if (headerErrors.Count > 0)
        {
            return (Array.Empty<BulkUploadRow>(), headerErrors);
        }

        var rows = new List<BulkUploadRow>();
        var errors = new List<BulkUserUploadError>();
        for (var rowNumber = HeaderRow + 1; rowNumber <= HeaderRow + MaxRows; rowNumber++)
        {
            var values = ExpectedHeaders
                .Select((_, index) => GetCell(cells, rowNumber, index + 1))
                .ToArray();
            if (values.All(string.IsNullOrWhiteSpace)) continue;

            var parsed = ParseRow(rowNumber, values, errors);
            if (parsed is not null)
            {
                rows.Add(parsed);
            }
        }

        if (rows.Count == 0 && errors.Count == 0)
        {
            errors.Add(new BulkUserUploadError(0, "Rows", string.Empty, "Workbook does not contain any user rows."));
        }

        return (rows, errors);
    }

    internal static SchedulePeriodRequest[] BuildRequestedPeriods(IEnumerable<BulkUploadRow> rows)
    {
        return rows
            .GroupBy(row => row.PeriodNumber)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var first = group.OrderBy(row => row.BlockNumber).First();
                return new SchedulePeriodRequest(
                    first.EffectiveFrom,
                    first.EffectiveTo,
                    first.ShiftTime,
                    group
                        .OrderBy(row => row.BlockNumber)
                        .Select(row => new ScheduleBlockRequest(row.Start, row.End, row.Days))
                        .ToArray(),
                    first.IsRepeating);
            })
            .ToArray();
    }

    internal static UserSchedulePeriod[] MergeSchedulePeriods(Guid userId, IReadOnlyCollection<UserSchedulePeriod> existing, IReadOnlyCollection<UserSchedulePeriod> incoming)
    {
        var preserved = existing.Select(ClonePeriodPreservingRange).ToList();
        foreach (var replacement in incoming.OrderBy(period => period.EffectiveFrom))
        {
            preserved = preserved
                .SelectMany(current => Subtract(current, replacement.EffectiveFrom, replacement.EffectiveTo))
                .ToList();
        }

        return preserved
            .Concat(incoming.Select(ClonePeriodPreservingRange))
            .Select(period => new UserSchedulePeriod
            {
                Id = period.Id == Guid.Empty ? Guid.NewGuid() : period.Id,
                UserId = userId,
                EffectiveFrom = period.EffectiveFrom.Date,
                EffectiveTo = period.EffectiveTo?.Date,
                ShiftTime = period.ShiftTime,
                BlocksJson = period.BlocksJson,
                IsRepeating = period.IsRepeating,
                CreatedAtUtc = period.CreatedAtUtc == default ? DateTime.UtcNow : period.CreatedAtUtc
            })
            .OrderBy(period => period.EffectiveFrom)
            .ThenBy(period => period.EffectiveTo ?? DateTime.MaxValue)
            .ToArray();
    }

    internal static string GenerateTempPassword()
    {
        var bytes = Guid.NewGuid().ToString("N")[..12];
        return $"Temp{bytes}1!";
    }

    private static IReadOnlyList<UserSchedulePeriod> Subtract(UserSchedulePeriod current, DateTime replacementStart, DateTime? replacementEnd)
    {
        if (!Overlaps(current.EffectiveFrom, current.EffectiveTo, replacementStart, replacementEnd))
        {
            return [current];
        }

        var result = new List<UserSchedulePeriod>();
        if (current.EffectiveFrom.Date < replacementStart.Date)
        {
            var leftEnd = replacementStart.Date.AddDays(-1);
            if (leftEnd >= current.EffectiveFrom.Date)
            {
                result.Add(ClonePeriodWithRange(current, current.EffectiveFrom.Date, leftEnd));
            }
        }

        if (replacementEnd.HasValue && (!current.EffectiveTo.HasValue || current.EffectiveTo.Value.Date > replacementEnd.Value.Date))
        {
            var rightStart = replacementEnd.Value.Date.AddDays(1);
            var rightEnd = current.EffectiveTo?.Date;
            if (!rightEnd.HasValue || rightEnd.Value >= rightStart)
            {
                result.Add(ClonePeriodWithRange(current, rightStart, rightEnd));
            }
        }

        return result;
    }

    private static bool Overlaps(DateTime startA, DateTime? endA, DateTime startB, DateTime? endB)
    {
        var maxStart = startA.Date >= startB.Date ? startA.Date : startB.Date;
        var minEnd = MinDate(endA?.Date, endB?.Date);
        return !minEnd.HasValue || maxStart <= minEnd.Value;
    }

    private static DateTime? MinDate(DateTime? a, DateTime? b)
    {
        if (!a.HasValue) return b;
        if (!b.HasValue) return a;
        return a.Value <= b.Value ? a : b;
    }

    private static UserSchedulePeriod ClonePeriodPreservingRange(UserSchedulePeriod period) => new()
    {
        Id = Guid.NewGuid(),
        UserId = period.UserId,
        EffectiveFrom = period.EffectiveFrom.Date,
        EffectiveTo = period.EffectiveTo?.Date,
        ShiftTime = period.ShiftTime,
        BlocksJson = period.BlocksJson,
        IsRepeating = period.IsRepeating,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static UserSchedulePeriod ClonePeriodWithRange(UserSchedulePeriod period, DateTime effectiveFrom, DateTime? effectiveTo) => new()
    {
        Id = Guid.NewGuid(),
        UserId = period.UserId,
        EffectiveFrom = effectiveFrom.Date,
        EffectiveTo = effectiveTo?.Date,
        ShiftTime = period.ShiftTime,
        BlocksJson = period.BlocksJson,
        IsRepeating = period.IsRepeating,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static BulkUploadRow? ParseRow(int rowNumber, string[] values, List<BulkUserUploadError> errors)
    {
        var email = values[2].Trim();
        string Required(int index)
        {
            var value = values[index].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new BulkUserUploadError(rowNumber, ExpectedHeaders[index], email, $"{ExpectedHeaders[index]} is required."));
            }
            return value;
        }

        var firstName = Required(0);
        var lastName = Required(1);
        email = Required(2).ToLowerInvariant();
        var roleText = Required(3);
        var location = Required(4);
        var companiesText = Required(5);
        var primaryCompany = Required(6);
        var operation = Required(7);
        var periodNumberText = Required(8);
        var effectiveFrom = Required(9);
        var effectiveTo = values[10].Trim();
        var shiftTime = Required(11);
        var isRepeatingText = values[12].Trim();
        var blockNumberText = Required(13);
        var start = Required(14);
        var end = Required(15);
        var daysText = Required(16);

        if (!RoleMap.TryGetValue(roleText, out var role))
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Role*", email, $"Role '{roleText}' is invalid. Use Employee, Manager, Admin, or Team Leader."));
        }

        var companies = ParseCsvList(companiesText);
        if (companies.Count == 0)
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Companies*", email, "At least one company is required."));
        }
        else if (!companies.Contains(primaryCompany, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Primary Company*", email, $"Primary Company '{primaryCompany}' must be included in Companies."));
        }

        var periodNumber = ParsePositiveInt(rowNumber, "Period Number*", email, periodNumberText, errors);
        var blockNumber = ParsePositiveInt(rowNumber, "Block Number*", email, blockNumberText, errors);
        if (!IsIsoDate(effectiveFrom))
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Effective From*", email, $"Effective From '{effectiveFrom}' must use yyyy-MM-dd."));
        }
        if (!string.IsNullOrWhiteSpace(effectiveTo) && !IsIsoDate(effectiveTo))
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Effective To", email, $"Effective To '{effectiveTo}' must use yyyy-MM-dd or be blank."));
        }
        if (!string.IsNullOrWhiteSpace(effectiveTo) && IsIsoDate(effectiveFrom) && IsIsoDate(effectiveTo) &&
            DateTime.ParseExact(effectiveTo, "yyyy-MM-dd", null).Date < DateTime.ParseExact(effectiveFrom, "yyyy-MM-dd", null).Date)
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Effective To", email, "Effective To cannot be before Effective From."));
        }

        if (!IsTime(start))
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Start*", email, $"Start '{start}' must use HH:mm."));
        }
        if (!IsTime(end))
        {
            errors.Add(new BulkUserUploadError(rowNumber, "End*", email, $"End '{end}' must use HH:mm."));
        }

        var days = daysText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (days.Length == 0)
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Days*", email, "At least one day is required."));
        }
        var invalidDay = days.FirstOrDefault(day => !ValidDays.Contains(day));
        if (invalidDay is not null)
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Days*", email, $"Day '{invalidDay}' is invalid. Use Mon,Tue,Wed,Thu,Fri,Sat,Sun."));
        }

        var isRepeating = false;
        if (!string.IsNullOrWhiteSpace(isRepeatingText) && !TryParseYesNo(isRepeatingText, out isRepeating))
        {
            errors.Add(new BulkUserUploadError(rowNumber, "Is Repeating?", email, $"Is Repeating '{isRepeatingText}' is invalid. Use Yes or No."));
        }

        return errors.Any(error => error.Row == rowNumber)
            ? null
            : new BulkUploadRow(rowNumber, firstName, lastName, email, role, location, companies.ToArray(), primaryCompany,
                operation, periodNumber, effectiveFrom, string.IsNullOrWhiteSpace(effectiveTo) ? null : effectiveTo,
                shiftTime, isRepeating, blockNumber, start, end, days);
    }

    private static int ParsePositiveInt(int row, string column, string email, string value, List<BulkUserUploadError> errors)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        errors.Add(new BulkUserUploadError(row, column, email, $"{column} must be a positive whole number."));
        return 0;
    }

    private static bool TryParseYesNo(string value, out bool parsed)
    {
        if (string.Equals(value.Trim(), "Yes", StringComparison.OrdinalIgnoreCase))
        {
            parsed = true;
            return true;
        }
        if (string.Equals(value.Trim(), "No", StringComparison.OrdinalIgnoreCase))
        {
            parsed = false;
            return true;
        }
        parsed = false;
        return false;
    }

    private static bool IsIsoDate(string value) =>
        DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", null, DateTimeStyles.None, out _);

    private static bool IsTime(string value) =>
        TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out _);

    private static List<string> ParseCsvList(string value)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < value.Length && value[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                AddCsvValue(result, current);
                continue;
            }

            current.Append(ch);
        }

        AddCsvValue(result, current);
        return result
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddCsvValue(ICollection<string> result, StringBuilder current)
    {
        var value = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
        current.Clear();
    }

    private static IReadOnlyList<BulkUserUploadError> ValidateHeaders(IReadOnlyDictionary<(int Row, int Column), string> cells)
    {
        var errors = new List<BulkUserUploadError>();
        for (var i = 0; i < ExpectedHeaders.Length; i++)
        {
            var actual = GetCell(cells, HeaderRow, i + 1);
            if (!string.Equals(actual, ExpectedHeaders[i], StringComparison.Ordinal))
            {
                errors.Add(new BulkUserUploadError(HeaderRow, ColumnName(i + 1), string.Empty, $"Expected header '{ExpectedHeaders[i]}' but found '{actual}'. Do not rename or move headers."));
            }
        }
        return errors;
    }

    private static Dictionary<(int Row, int Column), string> ReadCells(ZipArchiveEntry sheetEntry, string[] sharedStrings)
    {
        using var stream = sheetEntry.Open();
        var doc = XDocument.Load(stream);
        var result = new Dictionary<(int Row, int Column), string>();
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (var cell in doc.Descendants(ns + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            if (string.IsNullOrWhiteSpace(reference) || !TryParseCellReference(reference, out var row, out var col)) continue;

            var type = cell.Attribute("t")?.Value;
            string value;
            if (type == "s")
            {
                var raw = cell.Element(ns + "v")?.Value ?? string.Empty;
                value = int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Length ? sharedStrings[index] : string.Empty;
            }
            else if (type == "inlineStr")
            {
                value = string.Concat(cell.Descendants(ns + "t").Select(item => item.Value));
            }
            else
            {
                value = cell.Element(ns + "v")?.Value ?? string.Empty;
            }

            result[(row, col)] = CoerceValue(value, col);
        }

        return result;
    }

    private static string CoerceValue(string value, int column)
    {
        var trimmed = value.Trim();
        if (!double.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial))
        {
            return trimmed;
        }

        if ((column == 10 || column == 11) && serial > 20000 && serial < 60000)
        {
            return DateTime.FromOADate(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if ((column == 15 || column == 16) && serial >= 0 && serial < 1)
        {
            return DateTime.FromOADate(serial).ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        return trimmed;
    }

    private static string[] ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return Array.Empty<string>();

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        return doc.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string? ResolveFirstWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relsEntry is null) return null;

        using var workbookStream = workbookEntry.Open();
        using var relsStream = relsEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var rels = XDocument.Load(relsStream);
        var workbookNs = workbook.Root?.Name.Namespace ?? XNamespace.None;
        var relationshipNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var firstSheet = workbook.Descendants(workbookNs + "sheet").FirstOrDefault();
        var relId = firstSheet?.Attribute(relationshipNs + "id")?.Value;
        if (string.IsNullOrWhiteSpace(relId)) return null;

        var packageRelNs = rels.Root?.Name.Namespace ?? XNamespace.None;
        var target = rels.Descendants(packageRelNs + "Relationship")
            .FirstOrDefault(item => item.Attribute("Id")?.Value == relId)
            ?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target)) return null;

        var normalizedTarget = target.Replace('\\', '/').TrimStart('/');
        return normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalizedTarget
            : $"xl/{normalizedTarget}";
    }

    private static string GetCell(IReadOnlyDictionary<(int Row, int Column), string> cells, int row, int column) =>
        cells.TryGetValue((row, column), out var value) ? value.Trim() : string.Empty;

    private static bool TryParseCellReference(string reference, out int row, out int column)
    {
        row = 0;
        column = 0;
        var match = Regex.Match(reference, "^([A-Z]+)([0-9]+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        foreach (var ch in match.Groups[1].Value.ToUpperInvariant())
        {
            column = (column * 26) + ch - 'A' + 1;
        }
        return int.TryParse(match.Groups[2].Value, out row);
    }

    private static string ColumnName(int column)
    {
        var name = string.Empty;
        while (column > 0)
        {
            var modulo = (column - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            column = (column - modulo) / 26;
        }
        return name;
    }
}

internal record BulkUploadRow(
    int RowNumber,
    string FirstName,
    string LastName,
    string Email,
    int Role,
    string Location,
    string[] Companies,
    string PrimaryCompany,
    string Operation,
    int PeriodNumber,
    string EffectiveFrom,
    string? EffectiveTo,
    string ShiftTime,
    bool IsRepeating,
    int BlockNumber,
    string Start,
    string End,
    string[] Days);
