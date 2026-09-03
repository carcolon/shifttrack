using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api.IntegrationTests.Support;

internal sealed class InMemoryHolidayRepository : IHolidayRepository
{
    private readonly List<Holiday> _items =
    [
        new()
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
            Date = new DateTime(2026, 1, 1),
            Name = "Año Nuevo",
            CountryCode = "CO",
            IsActive = true,
            IsManual = false,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }
    ];

    public Task<IEnumerable<Holiday>> GetActiveByYearAsync(int year, string countryCode = "CO") =>
        Task.FromResult<IEnumerable<Holiday>>(_items
            .Where(item => item.IsActive && item.Date.Year == year && string.Equals(item.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Date)
            .ToArray());

    public Task<IEnumerable<Holiday>> GetActiveInRangeAsync(DateTime startDate, DateTime endDate, string countryCode = "CO") =>
        Task.FromResult<IEnumerable<Holiday>>(_items
            .Where(item => item.IsActive &&
                           item.Date.Date >= startDate.Date &&
                           item.Date.Date <= endDate.Date &&
                           string.Equals(item.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Date)
            .ToArray());
}
