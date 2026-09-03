using System.Text.Json;
using ShiftTrack.Api.IntegrationTests.Support;
using Xunit;

namespace ShiftTrack.Api.IntegrationTests;

public sealed class HolidaysEndpointIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public HolidaysEndpointIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHolidaysByYear_ReturnsColombianHolidays()
    {
        using var client = _factory.CreateAuthenticatedClient(role: 0, email: "employee@company.com");

        using var response = await client.GetAsync("/holidays?year=2026");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var firstItem = document.RootElement.GetProperty("items")[0];
        Assert.Equal("2026-01-01", firstItem.GetProperty("date").GetString());
        Assert.Equal("Año Nuevo", firstItem.GetProperty("name").GetString());
    }
}
