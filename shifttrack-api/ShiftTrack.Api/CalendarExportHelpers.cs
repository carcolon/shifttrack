using System.Globalization;
using ShiftTrack.Application;

namespace ShiftTrack.Api;

internal static class CalendarExportHelpers
{
    internal static CalendarExportFilter ReadFilter(HttpRequest request)
    {
        var requestedWeekStart = ResolveWeekStartFromQuery(request.Query["weekStart"].FirstOrDefault())
            ?? ResolveWeekStart(DateTime.UtcNow.Date);

        return new CalendarExportFilter
        {
            RequestedWeekStart = requestedWeekStart,
            Employee = request.Query["employee"].FirstOrDefault()?.Trim(),
            Shift = request.Query["shift"].FirstOrDefault()?.Trim(),
            Operation = request.Query["operation"].FirstOrDefault()?.Trim(),
            Company = request.Query["company"].FirstOrDefault()?.Trim(),
            Role = int.TryParse(request.Query["role"].FirstOrDefault()?.Trim(), out var role) && RoleHelpers.IsKnownRole(role)
                ? role
                : null
        };
    }

    internal static CalendarExportWindow ResolveWindow(CalendarExportFilter filter)
    {
        var hasFilter =
            !string.IsNullOrWhiteSpace(filter.Employee) ||
            !string.IsNullOrWhiteSpace(filter.Shift) ||
            !string.IsNullOrWhiteSpace(filter.Operation) ||
            !string.IsNullOrWhiteSpace(filter.Company) ||
            filter.Role.HasValue;

        var currentWeekStart = ResolveWeekStart(DateTime.UtcNow.Date);
        var useDefaultThreeWeekWindow =
            !hasFilter &&
            filter.RequestedWeekStart == currentWeekStart;

        var start = useDefaultThreeWeekWindow ? filter.RequestedWeekStart.AddDays(-14) : filter.RequestedWeekStart;
        var end = filter.RequestedWeekStart.AddDays(6);

        return new CalendarExportWindow
        {
            StartDate = start,
            EndDate = end,
            HasExplicitFilter = hasFilter
        };
    }

    internal static CalendarRow[] ApplyRowFilters(IEnumerable<CalendarRow> rows, CalendarExportFilter filter)
    {
        var employeeFilter = NormalizeSearch(filter.Employee ?? string.Empty);
        return rows.Where(row =>
        {
            if (!string.IsNullOrWhiteSpace(employeeFilter))
            {
                var haystack = NormalizeSearch($"{row.DisplayName} {row.Email}");
                if (!haystack.Contains(employeeFilter, StringComparison.Ordinal)) return false;
            }

            if (!string.IsNullOrWhiteSpace(filter.Shift) &&
                !string.Equals(row.ShiftTime, filter.Shift, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(filter.Operation) &&
                !string.Equals(row.Operation, filter.Operation, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(filter.Company) &&
                !string.Equals(row.Company, filter.Company, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (filter.Role.HasValue && row.Role != filter.Role.Value)
            {
                return false;
            }

            return true;
        }).ToArray();
    }

    internal static string[] ToExportDayValues(CalendarRow row)
    {
        return row.Cells
            .OrderBy(cell => cell.Date)
            .Take(7)
            .Select(FormatDayValue)
            .ToArray();
    }

    internal static string BuildWeekRangeLabel(DateTime weekStart) =>
        $"{weekStart:MM/dd/yyyy} - {weekStart.AddDays(6):MM/dd/yyyy}";

    internal static string FormatDayValue(CalendarCell cell)
    {
        if (cell.Type == "leave") return "PTO";
        if (cell.Type == "dayOff") return "Day Off";
        if (string.IsNullOrWhiteSpace(cell.Label)) return string.Empty;
        return cell.Label.Replace(" - ", "-", StringComparison.Ordinal);
    }

    internal static int ResolveWeekNumber(DateTime weekStart) =>
        ISOWeek.GetWeekOfYear(weekStart);
}

internal sealed record CalendarExportFilter
{
    internal DateTime RequestedWeekStart { get; init; }
    internal string? Employee { get; init; }
    internal string? Shift { get; init; }
    internal string? Operation { get; init; }
    internal string? Company { get; init; }
    internal int? Role { get; init; }
}

internal sealed record CalendarExportWindow
{
    internal DateTime StartDate { get; init; }
    internal DateTime EndDate { get; init; }
    internal bool HasExplicitFilter { get; init; }
}
