using System.Net;
using ClosedXML.Excel;
using ShiftTrack.Api.IntegrationTests.Support;
using ShiftTrack.Domain.Entities;
using Xunit;

namespace ShiftTrack.Api.IntegrationTests;

public sealed class CalendarExportIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public CalendarExportIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Calendar_ReturnsCalendar_ForEmployee()
    {
        using var client = _factory.CreateAuthenticatedClient(role: 0, email: "jhon.smith@company.com");

        using var response = await client.GetAsync("/calendar?weekStart=2026-03-09");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsWorkbook_ForManager()
    {
        using var client = _factory.CreateAuthenticatedClient(role: 1);

        using var response = await client.GetAsync("/calendar/export?weekStart=2026-03-09&operation=Leaders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        var payload = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(payload);
    }

    [Fact]
    public async Task Export_FiltersRowsToCallerCompanyScope()
    {
        var externalId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        await _factory.Repository.CreateUserAsync(new User
        {
            Id = externalId,
            TenantId = Guid.NewGuid(),
            ObjectId = Guid.NewGuid(),
            Email = "external.user@otherco.com",
            DisplayName = "External User",
            Role = 0,
            IsActive = true,
            PasswordHash = "hash",
            MustChangePassword = false,
            CreatedAtUtc = DateTime.UtcNow,
            Location = "COL",
            Company = "OtherCo",
            Operation = "Leaders",
            ShiftTime = "Morning",
            ScheduleBlocks = "[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]",
            SchedulePeriods =
            [
                new UserSchedulePeriod
                {
                    Id = Guid.NewGuid(),
                    UserId = externalId,
                    EffectiveFrom = new DateTime(2026, 3, 1),
                    ShiftTime = "Morning",
                    BlocksJson = "[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]",
                    CreatedAtUtc = DateTime.UtcNow
                }
            ]
        });
        using var client = _factory.CreateAuthenticatedClient(role: 1, email: "jhon.doe@company.com");

        using var response = await client.GetAsync("/calendar/export?weekStart=2026-03-09&operation=Leaders");

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(stream);
        var worksheetText = string.Join(
            " ",
            workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()).Select(cell => cell.GetString()));
        Assert.Contains("Jhon Doe", worksheetText);
        Assert.DoesNotContain("External User", worksheetText);
        Assert.DoesNotContain("OtherCo", worksheetText);
    }
}
