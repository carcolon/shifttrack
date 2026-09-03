using System.Net.Http.Json;
using System.Text.Json;
using ShiftTrack.Api.IntegrationTests.Support;
using Xunit;

namespace ShiftTrack.Api.IntegrationTests;

public sealed class AssistantEndpointIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AssistantEndpointIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Query_ReturnsFilterMembershipMatches_ForOperationQuestion()
    {
        using var client = _factory.CreateAuthenticatedClient(role: 1);

        using var response = await client.PostAsJsonAsync("/assistant/query", new
        {
            message = "who are leaders?",
            weekStart = "2026-03-09"
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("who_matches_filter", document.RootElement.GetProperty("intent").GetString());
        Assert.Equal("Jhon Doe", document.RootElement.GetProperty("matches")[0].GetProperty("displayName").GetString());
    }
}
