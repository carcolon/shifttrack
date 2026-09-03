using ShiftTrack.Application.Interfaces;

namespace ShiftTrack.Api;

internal interface IHolidayWorkflowService
{
    Task<IResult> GetHolidaysAsync(HttpRequest request);
}

internal sealed class HolidayWorkflowService : IHolidayWorkflowService
{
    private readonly IHolidayRepository _holidays;

    public HolidayWorkflowService(IHolidayRepository holidays)
    {
        _holidays = holidays;
    }

    public async Task<IResult> GetHolidaysAsync(HttpRequest request)
    {
        var countryCode = request.Query["countryCode"].ToString();
        if (string.IsNullOrWhiteSpace(countryCode)) countryCode = "CO";
        countryCode = countryCode.Trim().ToUpperInvariant();

        var yearQuery = request.Query["year"].ToString();
        if (int.TryParse(yearQuery, out var year))
        {
            var itemsByYear = await _holidays.GetActiveByYearAsync(year, countryCode);
            return Results.Ok(new
            {
                countryCode,
                year,
                items = itemsByYear.Select(h => new
                {
                    id = h.Id,
                    date = h.Date.ToString("yyyy-MM-dd"),
                    name = h.Name,
                    isManual = h.IsManual
                })
            });
        }

        var startDateQuery = request.Query["startDate"].ToString();
        var endDateQuery = request.Query["endDate"].ToString();
        if (DateTime.TryParse(startDateQuery, out var startDate) && DateTime.TryParse(endDateQuery, out var endDate))
        {
            var itemsByRange = await _holidays.GetActiveInRangeAsync(startDate.Date, endDate.Date, countryCode);
            return Results.Ok(new
            {
                countryCode,
                startDate = startDate.Date.ToString("yyyy-MM-dd"),
                endDate = endDate.Date.ToString("yyyy-MM-dd"),
                items = itemsByRange.Select(h => new
                {
                    id = h.Id,
                    date = h.Date.ToString("yyyy-MM-dd"),
                    name = h.Name,
                    isManual = h.IsManual
                })
            });
        }

        return Results.BadRequest(new ErrorResponse("Provide either year or startDate and endDate to query holidays."));
    }
}
