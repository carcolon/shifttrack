using System.Net.Http.Json;
using System.Text.Json;
using ShiftTrack.Api.IntegrationTests.Support;
using Xunit;

namespace ShiftTrack.Api.IntegrationTests;

public sealed class CoverageRulesEndpointIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public CoverageRulesEndpointIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PutAndGetCoverageRules_ReturnsResolvedRules_ForAdminInCompanyScope()
    {
        using var client = _factory.CreateAuthenticatedClient(role: 2, email: "admin@company.com");
        var rules = new[]
        {
            new { dayOfWeek = "Monday", expectedCoverage = 70, greenThreshold = 70, yellowThreshold = 60, isActive = true },
            new { dayOfWeek = "Tuesday", expectedCoverage = 71, greenThreshold = 71, yellowThreshold = 61, isActive = true },
            new { dayOfWeek = "Wednesday", expectedCoverage = 72, greenThreshold = 72, yellowThreshold = 62, isActive = true },
            new { dayOfWeek = "Thursday", expectedCoverage = 73, greenThreshold = 73, yellowThreshold = 63, isActive = true },
            new { dayOfWeek = "Friday", expectedCoverage = 74, greenThreshold = 74, yellowThreshold = 64, isActive = true },
            new { dayOfWeek = "Saturday", expectedCoverage = 75, greenThreshold = 75, yellowThreshold = 65, isActive = true },
            new { dayOfWeek = "Sunday", expectedCoverage = 76, greenThreshold = 76, yellowThreshold = 66, isActive = true }
        };

        using var putResponse = await client.PutAsJsonAsync("/coverage-rules", new
        {
            companyName = "Solvo Global",
            operationName = "Leaders",
            rules
        });
        putResponse.EnsureSuccessStatusCode();

        using var getResponse = await client.GetAsync("/coverage-rules?company=Solvo%20Global&operation=Leaders");
        getResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var monday = document.RootElement.EnumerateArray().First(item => item.GetProperty("dayOfWeek").GetString() == "Monday");
        Assert.Equal(70, monday.GetProperty("expectedCoverage").GetInt32());
        Assert.Equal("Leaders", monday.GetProperty("operationName").GetString());
    }
}
