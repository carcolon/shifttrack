using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class ReportEndpoints
{
    internal static WebApplication MapReportEndpoints(this WebApplication app)
    {
        app.MapGet("/reports/overview", async Task<IResult> (
            HttpContext httpContext,
            IUserRepository users,
            ICoverageRuleRepository coverageRules,
            string? company = null,
            string? period = null,
            string? startDate = null,
            string? endDate = null) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !RoleHelpers.IsAdmin(callerUser.Role))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var activeUsers = (await users.GetAllAsync()).Where(user => user.IsActive).ToArray();
            var inactiveUsers = (await users.GetInactiveAsync()).ToArray();
            var catalogCompanies = (await users.GetCompaniesAsync(includeInactive: false))
                .Where(item => item.IsActive)
                .Select(item => item.Name)
                .ToArray();

            var callerCompanies = callerUser.IsSystemHidden
                ? catalogCompanies.Concat(activeUsers.Select(user => user.Company))
                : CompanyScopeHelpers.ResolveCompanies(callerUser);
            var availableCompanies = callerCompanies
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (availableCompanies.Length == 0)
            {
                return Results.Ok(new ReportsOverviewResponse());
            }

            var requestedCompany = company?.Trim();
            if (!string.IsNullOrWhiteSpace(requestedCompany) &&
                !availableCompanies.Contains(requestedCompany, StringComparer.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var selectedCompany = !string.IsNullOrWhiteSpace(requestedCompany)
                ? availableCompanies.First(item => string.Equals(item, requestedCompany, StringComparison.OrdinalIgnoreCase))
                : availableCompanies[0];

            var range = ResolveReportRange(period, startDate, endDate);
            if (range is null)
            {
                return Results.BadRequest(new { message = "Invalid reporting date range." });
            }

            var reportStart = range.Value.Start;
            var reportEnd = range.Value.End;
            var reportDays = EnumerateInclusiveDates(reportStart, reportEnd);
            var scopedActiveUsers = activeUsers
                .Where(user => string.Equals(user.Company, selectedCompany, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var scopedInactiveUsers = inactiveUsers
                .Where(user => string.Equals(user.Company, selectedCompany, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var operationalActiveUsers = scopedActiveUsers
                .Where(IsOperationalUser)
                .ToArray();
            var operationalInactiveUsers = scopedInactiveUsers
                .Where(IsOperationalUser)
                .ToArray();
            var ptoUserIds = scopedActiveUsers
                .Concat(scopedInactiveUsers)
                .Where(user => !user.IsSystemHidden)
                .Select(user => user.Id)
                .ToHashSet();

            var currentWeekOverrides = (await users.GetScheduleOverridesAsync(reportStart, reportEnd)).ToArray();
            var currentOverrideMap = currentWeekOverrides.ToDictionary(
                item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}",
                item => item,
                StringComparer.OrdinalIgnoreCase);
            var companyRows = BuildRows(operationalActiveUsers, reportDays, currentOverrideMap);
            var companyRules = await coverageRules.ResolveRulesAsync(selectedCompany, null);
            var companyCoverage = BuildCoverage(reportDays, companyRows, companyRows.Length, companyRules);

            var operations = companyRows
                .Select(row => string.IsNullOrWhiteSpace(row.Operation) ? "Unassigned" : row.Operation.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var heatmap = new List<ReportCoverageHeatmapPoint>();
            var operationRisk = new List<ReportOperationRiskPoint>();
            foreach (var operation in operations)
            {
                var operationUsers = operationalActiveUsers
                    .Where(user => string.Equals(NormalizeOperation(user.Operation), operation, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var operationRows = BuildRows(operationUsers, reportDays, currentOverrideMap);
                var operationRules = await coverageRules.ResolveRulesAsync(selectedCompany, operation == "Unassigned" ? null : operation);
                var operationCoverage = BuildCoverage(reportDays, operationRows, operationRows.Length, operationRules);
                heatmap.AddRange(operationCoverage.Select(item => new ReportCoverageHeatmapPoint(
                    operation,
                    item.DayCode,
                    item.Date,
                    item.Coverage,
                    item.ExpectedCoverage,
                    item.StatusColor)));
                operationRisk.Add(new ReportOperationRiskPoint(
                    operation,
                    operationCoverage.Count(item => string.Equals(item.StatusColor, "red", StringComparison.OrdinalIgnoreCase)),
                    RoundOne(operationCoverage.Length == 0 ? 0 : operationCoverage.Average(item => item.Coverage))));
            }

            var trend = new List<ReportCoverageTrendPoint>();
            var trendAnchor = ResolveWeekStart(reportEnd);
            for (var offset = 11; offset >= 0; offset--)
            {
                var trendWeekStart = trendAnchor.AddDays(-7 * offset);
                var trendDays = Enumerable.Range(0, 7).Select(dayOffset => trendWeekStart.AddDays(dayOffset)).ToArray();
                var overrides = (await users.GetScheduleOverridesAsync(trendWeekStart, trendWeekStart.AddDays(6))).ToArray();
                var overrideMap = overrides.ToDictionary(
                    item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}",
                    item => item,
                    StringComparer.OrdinalIgnoreCase);
                var rows = BuildRows(operationalActiveUsers, trendDays, overrideMap);
                var coverage = BuildCoverage(trendDays, rows, rows.Length, companyRules);
                trend.Add(new ReportCoverageTrendPoint(
                    trendWeekStart.ToString("yyyy-MM-dd"),
                    RoundOne(coverage.Length == 0 ? 0 : coverage.Average(item => item.Coverage)),
                    coverage.Count(item => string.Equals(item.StatusColor, "red", StringComparison.OrdinalIgnoreCase))));
            }

            var ptoRequests = (await users.GetPtoRequestsAsync(null, 500))
                .Where(request => ptoUserIds.Contains(request.UserId))
                .Where(request => IsPtoRelevantForReportRange(request, reportStart, reportEnd))
                .ToArray();

            return Results.Ok(new ReportsOverviewResponse
            {
                SelectedCompany = selectedCompany,
                AvailableCompanies = availableCompanies,
                WeekStart = reportStart.ToString("yyyy-MM-dd"),
                WeekEnd = reportEnd.ToString("yyyy-MM-dd"),
                Kpis = new ReportsKpiResponse
                {
                    TotalActiveEmployees = operationalActiveUsers.Length,
                    AverageWeeklyCoverage = RoundOne(companyCoverage.Length == 0 ? 0 : companyCoverage.Average(item => item.Coverage)),
                    RiskDays = companyCoverage.Count(item => string.Equals(item.StatusColor, "red", StringComparison.OrdinalIgnoreCase)),
                    PendingPtoRequests = ptoRequests.Count(request => string.Equals(request.Status, "pending", StringComparison.OrdinalIgnoreCase)),
                    Operations = operations.Length
                },
                CoverageHeatmap = heatmap.ToArray(),
                CoverageTrend = trend.ToArray(),
                ExpectedVsActual = companyCoverage.Select(item => new ReportCoverageDayPoint(
                    item.DayCode,
                    item.Date,
                    item.ExpectedCoverage,
                    item.Coverage,
                    item.TotalAgents)).ToArray(),
                PtoByStatus = BuildMetricPoints(ptoRequests, request => NormalizeMetricLabel(request.Status)),
                PtoByType = BuildMetricPoints(ptoRequests, request => FormatPtoRequestTypeLabel(request.RequestType, request.RequestType)),
                HeadcountByOperation = operations.Select(operation => new ReportHeadcountPoint(
                    operation,
                    operationalActiveUsers.Count(user => string.Equals(NormalizeOperation(user.Operation), operation, StringComparison.OrdinalIgnoreCase)),
                    operationalInactiveUsers.Count(user => string.Equals(NormalizeOperation(user.Operation), operation, StringComparison.OrdinalIgnoreCase)))).ToArray(),
                TopRiskOperations = operationRisk
                    .OrderByDescending(item => item.RiskDays)
                    .ThenBy(item => item.AverageCoverage)
                    .Take(5)
                    .ToArray()
            });
        }).RequireAuthorization();

        return app;
    }

    private static CalendarRow[] BuildRows(User[] users, DateTime[] days, IReadOnlyDictionary<string, UserScheduleOverride> overrideMap)
    {
        var relevantUsers = users
            .Where(user => HasCalendarPresence(user, days, overrideMap))
            .ToArray();
        return relevantUsers.Select(user => BuildCalendarRow(user, days, overrideMap)).ToArray();
    }

    private static ReportMetricPoint[] BuildMetricPoints(PtoRequest[] requests, Func<PtoRequest, string> labelSelector) =>
        requests
            .GroupBy(labelSelector, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReportMetricPoint(group.Key, group.Count()))
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Label)
            .ToArray();

    private static (DateTime Start, DateTime End)? ResolveReportRange(string? period, string? startDate, string? endDate)
    {
        var today = DateTime.UtcNow.Date;
        var normalized = period?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "current-week";
        }

        DateTime start;
        DateTime end;
        switch (normalized)
        {
            case "current-week":
            case "currentweek":
                start = ResolveWeekStart(today);
                end = start.AddDays(6);
                break;
            case "previous-week":
            case "previousweek":
                start = ResolveWeekStart(today).AddDays(-7);
                end = start.AddDays(6);
                break;
            case "month":
            case "1-month":
            case "one-month":
                end = today;
                start = today.AddMonths(-1).AddDays(1);
                break;
            case "custom":
                if (!TryParseDate(startDate, out start) || !TryParseDate(endDate, out end))
                {
                    return null;
                }
                break;
            default:
                return null;
        }

        if (end < start)
        {
            return null;
        }

        if ((end - start).TotalDays > 92)
        {
            return null;
        }

        return (start, end);
    }

    private static DateTime[] EnumerateInclusiveDates(DateTime start, DateTime end) =>
        Enumerable.Range(0, (end - start).Days + 1)
            .Select(offset => start.AddDays(offset))
            .ToArray();

    private static bool TryParseDate(string? value, out DateTime date)
    {
        if (DateTime.TryParse(value, out var parsed))
        {
            date = parsed.Date;
            return true;
        }

        date = default;
        return false;
    }

    private static bool IsPtoRelevantForReportRange(PtoRequest request, DateTime start, DateTime end) =>
        DateOverlapsRange(request.StartDate, request.EndDate, start, end) ||
        DateIsInRange(request.CreatedAtUtc, start, end) ||
        DateIsInRange(request.UpdatedAtUtc, start, end) ||
        (request.ReviewedAtUtc.HasValue && DateIsInRange(request.ReviewedAtUtc.Value, start, end)) ||
        string.Equals(request.Status, "pending", StringComparison.OrdinalIgnoreCase);

    private static bool DateOverlapsRange(DateTime itemStart, DateTime itemEnd, DateTime start, DateTime end) =>
        itemStart.Date <= end && itemEnd.Date >= start;

    private static bool DateIsInRange(DateTime value, DateTime start, DateTime end) =>
        value.Date >= start && value.Date <= end;

    private static bool IsOperationalUser(User user) =>
        !user.IsSystemHidden && !RoleHelpers.IsAdmin(user.Role);

    private static string NormalizeOperation(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Unassigned" : normalized;
    }

    private static string NormalizeMetricLabel(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Unknown" : char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }

    private static double RoundOne(double value) => Math.Round(value, 1);

    private static async Task<User?> ResolveCallerUserAsync(HttpContext httpContext, IUserRepository users)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext))
        {
            return null;
        }

        if (callerContext.UserId.HasValue)
        {
            var byId = await users.GetByIdAsync(callerContext.UserId.Value);
            if (byId is not null && byId.IsActive)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(callerContext.Email)
            ? null
            : await users.GetByEmailAsync(callerContext.Email);
    }
}
