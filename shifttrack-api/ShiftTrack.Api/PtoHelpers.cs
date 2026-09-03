using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class PtoHelpers
{
    internal static string NormalizePtoRequestType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
        return normalized switch
        {
            "dayoffrequest" => "day_off",
            "dayoff" => "day_off",
            "sickleave" => "sick_leave",
            "maternityleave" => "maternity_leave",
            "birthday" => "birthday",
            "holiday" => "holiday",
            "familyday" => "family_day",
            "fmla" => "fmla",
            "vacations" => "vacations",
            "unpaidleave" => "unpaid_leave",
            _ => string.Empty
        };
    }

    internal static PtoRequestResponse ToPtoRequestResponse(PtoRequest request)
    {
        return new PtoRequestResponse
        {
            Id = request.Id,
            UserId = request.UserId,
            UserEmail = request.UserEmail,
            UserDisplayName = request.UserDisplayName,
            RequestType = request.RequestType,
            NumberOfDays = request.NumberOfDays,
            StartDate = request.StartDate.ToString("yyyy-MM-dd"),
            EndDate = request.EndDate.ToString("yyyy-MM-dd"),
            Comments = request.Comments,
            ReviewComments = request.ReviewComments,
            OverrideGroupId = request.OverrideGroupId,
            Status = request.Status,
            RequestedByEmail = request.RequestedByEmail,
            RequestedByName = request.RequestedByName,
            RequestedByRole = request.RequestedByRole,
            ReviewedByEmail = request.ReviewedByEmail,
            ReviewedByName = request.ReviewedByName,
            ReviewedByRole = request.ReviewedByRole,
            ReviewedAtUtc = request.ReviewedAtUtc?.ToString("O"),
            CreatedAtUtc = request.CreatedAtUtc.ToString("O")
        };
    }

    internal static string FormatPtoRequestTypeLabel(string? requestType, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(requestType) ? fallback : requestType.Trim().ToLowerInvariant();
        normalized = normalized.Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "dayoffrequest" or "dayoff" or "day_off" => "Day Off",
            "sickleave" or "sick_leave" => "Sick Leave",
            "maternityleave" or "maternity_leave" => "Maternity Leave",
            "paternityleave" or "paternity_leave" => "Paternity Leave",
            "birthday" => "Birthday",
            "holiday" => "Holiday",
            "familyday" or "family_day" => "Family Day",
            "fmla" => "FMLA",
            "vacations" => "Vacations",
            "unpaidleave" or "unpaid_leave" => "Unpaid Leave",
            "absence" => "Absence",
            _ => "PTO"
        };
    }
}
