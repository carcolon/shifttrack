using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class CalendarHelpers
{
    internal static bool HasCalendarPresence(User user, IEnumerable<DateTime> days, IReadOnlyDictionary<string, UserScheduleOverride> overrideMap)
    {
        var orderedDays = days.OrderBy(day => day).ToArray();
        if (orderedDays.Length == 0) return false;

        // Legacy fallback: users without migrated periods still behave as always-scheduled profiles.
        if ((user.SchedulePeriods is null || user.SchedulePeriods.Count == 0) &&
            (!string.IsNullOrWhiteSpace(user.ScheduleBlocks) || !string.IsNullOrWhiteSpace(user.ShiftTime)))
        {
            return true;
        }

        foreach (var day in orderedDays)
        {
            if (SchedulePeriodHelpers.ResolveSchedulePeriodForDate(user, day) is not null)
            {
                return true;
            }

            var overrideKey = $"{user.Id:N}|{day:yyyy-MM-dd}";
            if (overrideMap.ContainsKey(overrideKey))
            {
                return true;
            }
        }

        return false;
    }

    internal static DateTime? ResolveWeekStartFromQuery(string? weekStartQuery)
    {
        if (string.IsNullOrWhiteSpace(weekStartQuery)) return null;
        if (!DateTime.TryParseExact(
            weekStartQuery.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            return null;
        }

        return ResolveWeekStart(parsed.Date);
    }

    internal static async Task RebuildWeekSnapshotAsync(IUserRepository users, ICoverageRuleRepository coverageRules, DateTime weekStart)
    {
        var start = ResolveWeekStart(weekStart.Date);
        var days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToArray();
        var allUsers = (await users.GetAllAsync()).Where(u => u.IsActive).ToArray();
        var overrides = (await users.GetScheduleOverridesAsync(start, start.AddDays(6))).ToArray();
        var overrideMap = overrides.ToDictionary(
            o => $"{o.UserId:N}|{o.OverrideDate:yyyy-MM-dd}",
            o => o,
            StringComparer.OrdinalIgnoreCase);

        var relevantUsers = allUsers
            .Where(u => HasCalendarPresence(u, days, overrideMap))
            .ToArray();

        var rows = relevantUsers.Select(u => BuildCalendarRow(u, days, overrideMap)).ToArray();
        var scope = ResolveCoverageScope(relevantUsers);
        var resolvedRules = await coverageRules.ResolveRulesAsync(scope.CompanyName, scope.OperationName);
        var coverage = BuildCoverage(days, rows, relevantUsers.Length, resolvedRules);

        await users.UpsertCoverageSnapshotAsync(new WeeklyCoverageSnapshot
        {
            WeekStartDate = start,
            PayloadJson = JsonSerializer.Serialize(coverage),
            ItemsJson = JsonSerializer.Serialize(rows),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    internal static DateTime ResolveWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    internal static string NormalizeSearch(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var formD = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    internal static CoverageSummary[] BuildCoverage(
        IEnumerable<DateTime> days,
        IEnumerable<CalendarRow> rows,
        int totalAccountAgents,
        IEnumerable<CoverageRule>? rules = null)
    {
        var ruleMap = CoverageRuleDefaults.ToRuleMap(rules);
        var rowArray = rows.ToArray();

        return days.Select(day =>
        {
            var dayKey = day.ToString("yyyy-MM-dd");
            var totalAgents = rowArray.Count(r => IsWorkingCell(r.Cells.FirstOrDefault(c => c.Date == dayKey)));
            var coverage = totalAccountAgents == 0
                ? 0
                : Math.Round((totalAgents * 100.0) / totalAccountAgents, 1);

            var rule = ruleMap[day.DayOfWeek];
            var color = ResolveCoverageColor(rule, coverage);
            return new CoverageSummary
            {
                Date = day.ToString("yyyy-MM-dd"),
                DayCode = ApiHelpers.DayAbbrev(day.DayOfWeek),
                ExpectedCoverage = rule.ExpectedCoverage,
                Coverage = coverage,
                TotalAgents = totalAgents,
                StatusColor = color
            };
        }).ToArray();
    }

    internal static CalendarRow BuildCalendarRow(User user, IEnumerable<DateTime> days, IReadOnlyDictionary<string, UserScheduleOverride> overrideMap)
    {
        var orderedDays = days.OrderBy(day => day).ToArray();
        var weekStart = orderedDays.FirstOrDefault();
        var basePeriod = weekStart == default ? null : SchedulePeriodHelpers.ResolveSchedulePeriodForDate(user, weekStart);
        var baseShiftTime = basePeriod?.ShiftTime ?? user.ShiftTime;

        var schedule = orderedDays.Select(d => new CalendarCell
        {
            Date = d.ToString("yyyy-MM-dd"),
            Label = "Day Off",
            DurationHours = 0,
            Type = "dayOff",
            ShiftTime = baseShiftTime
        }).ToDictionary(c => c.Date, c => c);

        foreach (var day in orderedDays)
        {
            var period = SchedulePeriodHelpers.ResolveSchedulePeriodForDate(user, day);
            var shiftTime = period?.ShiftTime ?? user.ShiftTime;
            var blocks = period is not null
                ? SchedulePeriodHelpers.DeserializeBlocks(period.BlocksJson)
                : (string.IsNullOrWhiteSpace(user.ScheduleBlocks)
                    ? Array.Empty<ScheduleBlockDto>()
                    : JsonSerializer.Deserialize<IEnumerable<ScheduleBlockDto>>(user.ScheduleBlocks!) ?? Array.Empty<ScheduleBlockDto>());

            foreach (var b in blocks)
            {
                if (string.IsNullOrWhiteSpace(b.Start) || string.IsNullOrWhiteSpace(b.End) || b.Days is null) continue;
                if (!b.Days.Any(dayCode => ApiHelpers.DayAbbrev(day.DayOfWeek).Equals(dayCode, StringComparison.OrdinalIgnoreCase))) continue;

                var duration = ApiHelpers.TryDurationHours(b.Start, b.End);
                schedule[day.ToString("yyyy-MM-dd")] = new CalendarCell
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Label = $"{b.Start} - {b.End}",
                    DurationHours = duration,
                    Type = string.Equals(shiftTime, "Late", StringComparison.OrdinalIgnoreCase) ? "shiftLate" : "shiftMorning",
                    ShiftTime = shiftTime
                };
            }
        }

        foreach (var day in orderedDays)
        {
            var dayKey = day.ToString("yyyy-MM-dd");
            var overrideKey = $"{user.Id:N}|{dayKey}";
            if (!overrideMap.TryGetValue(overrideKey, out var entry)) continue;

            var previousKey = $"{user.Id:N}|{day.AddDays(-1):yyyy-MM-dd}";
            var isPtoStart = entry.GroupId.HasValue &&
                             (!overrideMap.TryGetValue(previousKey, out var previousEntry) || previousEntry.GroupId != entry.GroupId);

            schedule[dayKey] = MapOverrideToCell(entry, dayKey, schedule[dayKey].ShiftTime, isPtoStart);
        }

        return new CalendarRow
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? user.Email,
            Email = user.Email,
            Role = user.Role,
            Location = user.Location,
            Company = user.Company,
            Operation = user.Operation,
            ShiftTime = baseShiftTime,
            Cells = schedule.Values.OrderBy(c => c.Date).ToArray()
        };
    }

    internal static CalendarCell ResolveCalendarCellForDate(User user, DateTime date, IReadOnlyDictionary<string, UserScheduleOverride> overrideMap)
    {
        var weekStart = ResolveWeekStart(date.Date);
        var days = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset)).ToArray();
        var row = BuildCalendarRow(user, days, overrideMap);
        return row.Cells.First(cell => cell.Date == date.ToString("yyyy-MM-dd"));
    }

    internal static bool IsWorkingCell(CalendarCell? cell)
    {
        if (cell is null) return false;
        return cell.Type is "shiftMorning" or "shiftLate";
    }

    private static CalendarCell MapOverrideToCell(UserScheduleOverride entry, string dayKey, string defaultShiftTime, bool isPtoStart)
    {
        var normalized = (entry.EntryType ?? string.Empty).Trim().ToLowerInvariant();
        var label = string.IsNullOrWhiteSpace(entry.Label) ? string.Empty : entry.Label.Trim();

        if (normalized is "dayoff" or "day_off")
        {
            return new CalendarCell
            {
                Date = dayKey,
                Label = string.IsNullOrWhiteSpace(label) ? "Day Off" : label,
                DurationHours = 0,
                Type = "dayOff",
                ShiftTime = defaultShiftTime,
                PtoGroupId = entry.GroupId?.ToString(),
                PtoRequestType = entry.RequestType,
                PtoComments = entry.Comments,
                IsPtoStart = isPtoStart
            };
        }

        var isLeave = normalized is
            "pto" or
            "vacations" or
            "absence" or
            "sickleave" or
            "sick_leave" or
            "maternityleave" or
            "maternity_leave" or
            "paternityleave" or
            "paternity_leave" or
            "birthday" or
            "holiday" or
            "familyday" or
            "family_day" or
            "fmla" or
            "unpaidleave" or
            "unpaid_leave";
        if (isLeave)
        {
            var ptoTypeLabel = FormatPtoRequestTypeLabel(entry.RequestType, normalized);
            return new CalendarCell
            {
                Date = dayKey,
                Label = string.IsNullOrWhiteSpace(label) ? $"PTO: {ptoTypeLabel}" : $"{label}: {ptoTypeLabel}",
                DurationHours = 0,
                Type = "leave",
                ShiftTime = defaultShiftTime,
                PtoGroupId = entry.GroupId?.ToString(),
                PtoRequestType = entry.RequestType,
                PtoComments = entry.Comments,
                IsPtoStart = isPtoStart
            };
        }

        var start = entry.StartTime?.Trim() ?? string.Empty;
        var end = entry.EndTime?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end))
        {
            var isLate = normalized is "shiftlate" or "shift_late" or "late" ||
                string.Equals(entry.RequestType, "shiftLate", StringComparison.OrdinalIgnoreCase);
            var shiftType = isLate ? "shiftLate" : "shiftMorning";
            return new CalendarCell
            {
                Date = dayKey,
                Label = $"{start} - {end}",
                DurationHours = ApiHelpers.TryDurationHours(start, end),
                Type = shiftType,
                ShiftTime = isLate ? "Late" : "Morning",
                IsDailyScheduleOverride = normalized is "daily_schedule",
                ScheduleOverrideComments = normalized is "daily_schedule" ? entry.Comments : null
            };
        }

        return new CalendarCell
        {
            Date = dayKey,
            Label = string.IsNullOrWhiteSpace(label) ? "Day Off" : label,
            DurationHours = 0,
            Type = "dayOff",
            ShiftTime = defaultShiftTime
        };
    }

    internal static (string CompanyName, string? OperationName) ResolveCoverageScope(IEnumerable<User> users)
    {
        var userArray = users.ToArray();
        var companies = userArray
            .Select(user => user.Company?.Trim())
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var operations = userArray
            .Select(user => user.Operation?.Trim())
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return (
            companies.Length == 1 ? companies[0]! : string.Empty,
            operations.Length == 1 ? operations[0] : null);
    }

    internal static (string CompanyName, string? OperationName) ResolveCoverageScope(IEnumerable<CalendarRow> rows)
    {
        var rowArray = rows.ToArray();
        var companies = rowArray
            .Select(row => row.Company?.Trim())
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var operations = rowArray
            .Select(row => row.Operation?.Trim())
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return (
            companies.Length == 1 ? companies[0]! : string.Empty,
            operations.Length == 1 ? operations[0] : null);
    }

    private static string ResolveCoverageColor(CoverageRule rule, double coverage)
    {
        if (coverage >= rule.GreenThreshold) return "green";
        if (coverage >= rule.YellowThreshold) return "yellow";
        return "red";
    }
}
