using System.Text.Json;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class SchedulePeriodHelpers
{
    internal static SchedulePeriodDto[] ToSchedulePeriodDtos(IReadOnlyCollection<UserSchedulePeriod> periods)
    {
        return periods
            .OrderBy(period => period.EffectiveFrom)
            .Select(period => new SchedulePeriodDto(
                period.EffectiveFrom.ToString("yyyy-MM-dd"),
                period.EffectiveTo?.ToString("yyyy-MM-dd"),
                period.ShiftTime,
                DeserializeBlocks(period.BlocksJson),
                period.IsRepeating))
            .ToArray();
    }

    internal static UserSchedulePeriod[] BuildSchedulePeriods(Guid userId, IEnumerable<SchedulePeriodRequest> requests)
    {
        return requests
            .Select(request => new UserSchedulePeriod
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EffectiveFrom = DateOnlyString(request.EffectiveFrom),
                EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : DateOnlyString(request.EffectiveTo),
                ShiftTime = request.ShiftTime.Trim(),
                BlocksJson = JsonSerializer.Serialize(request.ScheduleBlocks ?? Array.Empty<ScheduleBlockRequest>()),
                IsRepeating = request.IsRepeating,
                CreatedAtUtc = DateTime.UtcNow
            })
            .OrderBy(period => period.EffectiveFrom)
            .ToArray();
    }

    internal static string? ValidateSchedulePeriods(IEnumerable<SchedulePeriodRequest>? schedulePeriods)
    {
        if (schedulePeriods is null || !schedulePeriods.Any()) return "Please add at least one schedule period.";

        var periods = schedulePeriods.Select((period, index) => new
        {
            Index = index,
            Period = period,
            Start = TryParseDate(period.EffectiveFrom),
            End = string.IsNullOrWhiteSpace(period.EffectiveTo) ? (DateTime?)null : TryParseDate(period.EffectiveTo)
        }).ToArray();

        foreach (var entry in periods)
        {
            if (!entry.Start.HasValue)
            {
                return $"Schedule period {entry.Index + 1} has an invalid effective from date.";
            }

            if (entry.End.HasValue && entry.End.Value.Date < entry.Start.Value.Date)
            {
                return $"Schedule period {entry.Index + 1} ends before it starts.";
            }

            if (entry.Period.IsRepeating && !entry.End.HasValue)
            {
                return $"Schedule period {entry.Index + 1} must use Valid until when automatic repeat is enabled.";
            }

            var duplicateDay = ApiHelpers.FindDuplicateDay(entry.Period.ScheduleBlocks);
            if (duplicateDay is not null)
            {
                return $"Schedule period {entry.Index + 1}: {duplicateDay}";
            }
        }

        var repeatingPeriods = periods.Where(entry => entry.Period.IsRepeating).ToArray();
        if (repeatingPeriods.Length > 0 && repeatingPeriods.Length < 2)
        {
            return "Automatically repeat shift period requires at least two schedule periods.";
        }

        if (repeatingPeriods.Length > 0 && repeatingPeriods.Length != periods.Length)
        {
            return "Automatically repeat shift period must include every schedule period in the cycle.";
        }

        for (var i = 0; i < periods.Length; i++)
        {
            for (var j = i + 1; j < periods.Length; j++)
            {
                if (Overlaps(periods[i].Start!.Value, periods[i].End, periods[j].Start!.Value, periods[j].End))
                {
                    return "Schedule periods cannot overlap.";
                }
            }
        }

        return null;
    }

    internal static UserSchedulePeriod? ResolveSchedulePeriodForDate(User user, DateTime date)
    {
        var target = date.Date;
        var directPeriod = user.SchedulePeriods
            .Where(period => period.EffectiveFrom.Date <= target &&
                             (!period.EffectiveTo.HasValue || period.EffectiveTo.Value.Date >= target))
            .OrderByDescending(period => period.EffectiveFrom)
            .ThenByDescending(period => period.CreatedAtUtc)
            .FirstOrDefault();

        if (directPeriod is not null)
        {
            return directPeriod;
        }

        var repeatingPeriods = user.SchedulePeriods
            .Where(period => period.IsRepeating && period.EffectiveTo.HasValue)
            .OrderBy(period => period.EffectiveFrom)
            .ThenBy(period => period.CreatedAtUtc)
            .ToArray();
        if (repeatingPeriods.Length < 2) return null;

        var cycleStart = repeatingPeriods[0].EffectiveFrom.Date;
        var cycleEnd = repeatingPeriods.Max(period => period.EffectiveTo!.Value.Date);
        if (target <= cycleEnd) return null;

        var cycleLength = (cycleEnd - cycleStart).Days + 1;
        if (cycleLength <= 0) return null;

        var offset = (target - cycleStart).Days % cycleLength;
        var mappedDate = cycleStart.AddDays(offset);
        return repeatingPeriods
            .Where(period => period.EffectiveFrom.Date <= mappedDate &&
                             period.EffectiveTo!.Value.Date >= mappedDate)
            .OrderByDescending(period => period.EffectiveFrom)
            .ThenByDescending(period => period.CreatedAtUtc)
            .FirstOrDefault();
    }

    internal static ScheduleBlockDto[] DeserializeBlocks(string? blocksJson)
    {
        if (string.IsNullOrWhiteSpace(blocksJson)) return Array.Empty<ScheduleBlockDto>();
        return JsonSerializer.Deserialize<ScheduleBlockDto[]>(blocksJson) ?? Array.Empty<ScheduleBlockDto>();
    }

    internal static string? BuildLegacyScheduleBlocksJson(IReadOnlyCollection<UserSchedulePeriod> periods)
    {
        var first = periods.OrderBy(period => period.EffectiveFrom).FirstOrDefault();
        return first?.BlocksJson;
    }

    internal static string BuildLegacyShiftTime(IReadOnlyCollection<UserSchedulePeriod> periods)
    {
        var first = periods.OrderBy(period => period.EffectiveFrom).FirstOrDefault();
        return first?.ShiftTime ?? string.Empty;
    }

    private static bool Overlaps(DateTime startA, DateTime? endA, DateTime startB, DateTime? endB)
    {
        var maxStart = startA >= startB ? startA : startB;
        var minEnd = MinDate(endA, endB);
        return !minEnd.HasValue || maxStart.Date <= minEnd.Value.Date;
    }

    private static DateTime? MinDate(DateTime? a, DateTime? b)
    {
        if (!a.HasValue) return b;
        if (!b.HasValue) return a;
        return a.Value <= b.Value ? a : b;
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : null;
    }

    private static DateTime DateOnlyString(string value)
    {
        return DateTime.ParseExact(value.Trim(), "yyyy-MM-dd", null).Date;
    }
}
