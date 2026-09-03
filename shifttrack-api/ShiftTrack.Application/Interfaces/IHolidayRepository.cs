using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Application.Interfaces;

public interface IHolidayRepository
{
    Task<IEnumerable<Holiday>> GetActiveByYearAsync(int year, string countryCode = "CO");
    Task<IEnumerable<Holiday>> GetActiveInRangeAsync(DateTime startDate, DateTime endDate, string countryCode = "CO");
}
