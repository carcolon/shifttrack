using ClosedXML.Excel;
using ShiftTrack.Application;

namespace ShiftTrack.Api;

internal sealed partial class ScheduleWorkflowService
{
    public async Task<IResult> ExportCalendarAsync(HttpContext httpContext, HttpRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var filter = CalendarExportHelpers.ReadFilter(request);
        var window = CalendarExportHelpers.ResolveWindow(filter);
        var templatePath = Path.Combine(_environment.ContentRootPath, "Templates", "shifttrack-export-template.xlsx");
        if (!File.Exists(templatePath))
        {
            return Results.Problem("Export template not found on server.");
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var allUsers = (await _users.GetAllAsync())
            .Where(user => user.IsActive)
            .Where(user => IsInCallerCompanyScope(callerUser, user))
            .ToArray();
        var overrides = (await _users.GetScheduleOverridesAsync(window.StartDate, window.EndDate)).ToArray();
        var overrideMap = overrides.ToDictionary(
            item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}",
            item => item,
            StringComparer.OrdinalIgnoreCase);

        using var workbook = new XLWorkbook(templatePath);
        var worksheet = workbook.Worksheets.First();
        var rowIndex = 2;

        foreach (var weekStart in EachWeekStart(window.StartDate, window.EndDate))
        {
            var days = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset)).ToArray();
            var relevantUsers = allUsers
                .Where(user => HasCalendarPresence(user, days, overrideMap))
                .ToArray();

            var rows = relevantUsers
                .Select(user => BuildCalendarRow(user, days, overrideMap))
                .ToArray();

            var filteredRows = CalendarExportHelpers.ApplyRowFilters(rows, filter)
                .OrderBy(row => row.ShiftTime, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var row in filteredRows)
            {
                var dayValues = CalendarExportHelpers.ToExportDayValues(row);
                worksheet.Cell(rowIndex, 1).Value = row.DisplayName;
                worksheet.Cell(rowIndex, 2).Value = row.Operation;
                worksheet.Cell(rowIndex, 3).Value = row.Location;
                worksheet.Cell(rowIndex, 4).Value = row.Company;
                worksheet.Cell(rowIndex, 5).Value = row.ShiftTime;
                worksheet.Cell(rowIndex, 6).Value = CalendarExportHelpers.ResolveWeekNumber(weekStart);
                worksheet.Cell(rowIndex, 7).Value = CalendarExportHelpers.BuildWeekRangeLabel(weekStart);

                for (var i = 0; i < dayValues.Length; i++)
                {
                    worksheet.Cell(rowIndex, 8 + i).Value = dayValues[i];
                }

                rowIndex += 1;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"shifttrack-calendar-export-{window.StartDate:yyyyMMdd}-{window.EndDate:yyyyMMdd}.xlsx";
        return Results.File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static IEnumerable<DateTime> EachWeekStart(DateTime startDate, DateTime endDate)
    {
        for (var current = ResolveWeekStart(startDate); current <= endDate; current = current.AddDays(7))
        {
            yield return current;
        }
    }
}
