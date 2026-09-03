using Microsoft.EntityFrameworkCore;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;
using ShiftTrack.Infrastructure.Persistence;

namespace ShiftTrack.Infrastructure.Repositories;

public sealed class EfHolidayRepository(ShiftTrackDbContext dbContext) : IHolidayRepository
{
    public async Task<IEnumerable<Holiday>> GetActiveByYearAsync(int year, string countryCode = "CO") =>
        await dbContext.Holidays
            .AsNoTracking()
            .Where(holiday => holiday.IsActive &&
                              holiday.CountryCode == countryCode &&
                              holiday.Date.Year == year)
            .OrderBy(holiday => holiday.Date)
            .ToArrayAsync();

    public async Task<IEnumerable<Holiday>> GetActiveInRangeAsync(DateTime startDate, DateTime endDate, string countryCode = "CO")
    {
        var from = startDate.Date;
        var to = endDate.Date;

        return await dbContext.Holidays
            .AsNoTracking()
            .Where(holiday => holiday.IsActive &&
                              holiday.CountryCode == countryCode &&
                              holiday.Date >= from &&
                              holiday.Date <= to)
            .OrderBy(holiday => holiday.Date)
            .ToArrayAsync();
    }
}
