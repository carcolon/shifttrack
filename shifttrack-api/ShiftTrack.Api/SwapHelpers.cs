using System.Text.Json;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class SwapHelpers
{
    internal static string NormalizeSwapRequestType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "" => string.Empty,
            "swap" or "swap_shift" or "swap shift" => "swap_shift",
            _ => string.Empty
        };
    }

    internal static SwapRequestResponse ToSwapRequestResponse(SwapRequest request)
    {
        var requestedDates = DeserializeDateList(request.RequestedDatesJson);
        var targetDates = DeserializeDateList(request.TargetDatesJson);
        var pairs = DeserializePairs(request.PairingsJson);
        var weeklyHours = DeserializeWeeklyHours(request.WeeklyHoursJson);

        return new SwapRequestResponse
        {
            Id = request.Id,
            RequestedByUserId = request.RequestedByUserId,
            RequestedByEmail = request.RequestedByEmail,
            RequestedByDisplayName = request.RequestedByDisplayName,
            RequestedByRole = request.RequestedByRole,
            TargetUserId = request.TargetUserId,
            TargetUserEmail = request.TargetUserEmail,
            TargetUserDisplayName = request.TargetUserDisplayName,
            TargetUserRole = request.TargetUserRole,
            SwapDate = request.SwapDate.ToString("yyyy-MM-dd"),
            RequestedDates = requestedDates,
            TargetDates = targetDates,
            AppliedGroupId = request.AppliedGroupId,
            Pairs = pairs.Select(ToPairResponse).ToArray(),
            RequestType = request.RequestType,
            Comments = request.Comments,
            ReviewComments = request.ReviewComments,
            WeeklyHours = weeklyHours.Select(item => new SwapWeeklyHoursResponse
            {
                WeekStart = item.WeekStart,
                RequesterHours = item.RequesterHours,
                TargetHours = item.TargetHours,
                LimitHours = item.LimitHours
            }).ToArray(),
            ExceedsWeeklyHoursLimit = weeklyHours.Any(item => item.RequesterHours > item.LimitHours || item.TargetHours > item.LimitHours),
            Status = request.Status,
            ReviewedByEmail = request.ReviewedByEmail,
            ReviewedByName = request.ReviewedByName,
            ReviewedByRole = request.ReviewedByRole,
            ReviewedAtUtc = request.ReviewedAtUtc?.ToString("O"),
            CreatedAtUtc = request.CreatedAtUtc.ToString("O")
        };
    }

    internal static string SerializeDateList(IEnumerable<DateTime> dates) =>
        JsonSerializer.Serialize(dates.OrderBy(item => item).Select(item => item.ToString("yyyy-MM-dd")).ToArray());

    internal static string[] DeserializeDateList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    internal static string SerializePairs(IEnumerable<SwapPairSnapshot> pairs) =>
        JsonSerializer.Serialize(pairs.ToArray());

    internal static string SerializeWeeklyHours(IEnumerable<SwapWeeklyHoursSnapshot> weeklyHours) =>
        JsonSerializer.Serialize(weeklyHours.ToArray());

    internal static SwapWeeklyHoursSnapshot[] DeserializeWeeklyHours(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SwapWeeklyHoursSnapshot>();
        try
        {
            return JsonSerializer.Deserialize<SwapWeeklyHoursSnapshot[]>(json) ?? Array.Empty<SwapWeeklyHoursSnapshot>();
        }
        catch (JsonException)
        {
            return Array.Empty<SwapWeeklyHoursSnapshot>();
        }
    }

    internal static SwapPairSnapshot[] DeserializePairs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SwapPairSnapshot>();
        try
        {
            return JsonSerializer.Deserialize<SwapPairSnapshot[]>(json) ?? Array.Empty<SwapPairSnapshot>();
        }
        catch (JsonException)
        {
            return Array.Empty<SwapPairSnapshot>();
        }
    }

    internal static SwapPairResponse ToPairResponse(SwapPairSnapshot pair) => new()
    {
        RequesterCurrent = ToEntryResponse(pair.RequesterCurrent),
        TargetCurrent = ToEntryResponse(pair.TargetCurrent),
        RequesterResult = ToEntryResponse(pair.RequesterResult),
        TargetResult = ToEntryResponse(pair.TargetResult)
    };

    internal static SwapScheduleEntryResponse ToEntryResponse(SwapScheduleSnapshot entry) => new()
    {
        Date = entry.Date,
        Label = entry.Label,
        ShiftTime = entry.ShiftTime,
        DurationHours = entry.DurationHours,
        Type = entry.Type
    };

    internal static string[] BuildEmailSummaryLines(IEnumerable<SwapPairSnapshot> pairs) =>
        pairs.Select(pair =>
            $"{pair.RequesterCurrent.Date}: {pair.RequesterCurrent.OwnerName} {pair.RequesterCurrent.Label} -> {pair.RequesterResult.Label} | " +
            $"{pair.TargetCurrent.Date}: {pair.TargetCurrent.OwnerName} {pair.TargetCurrent.Label} -> {pair.TargetResult.Label}")
        .ToArray();
}

internal record SwapScheduleSnapshot
{
    public string OwnerName { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string ShiftTime { get; init; } = string.Empty;
    public double DurationHours { get; init; }
    public string Type { get; init; } = string.Empty;
}

internal record SwapPairSnapshot
{
    public SwapScheduleSnapshot RequesterCurrent { get; init; } = new();
    public SwapScheduleSnapshot TargetCurrent { get; init; } = new();
    public SwapScheduleSnapshot RequesterResult { get; init; } = new();
    public SwapScheduleSnapshot TargetResult { get; init; } = new();
}

internal record SwapWeeklyHoursSnapshot
{
    public string WeekStart { get; init; } = string.Empty;
    public double RequesterHours { get; init; }
    public double TargetHours { get; init; }
    public double LimitHours { get; init; } = 45;
}
