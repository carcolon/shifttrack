using System.Net.Http.Json;
using System.Text.Json;
using ShiftTrack.Api.IntegrationTests.Support;
using Xunit;

namespace ShiftTrack.Api.IntegrationTests;

public sealed class UsersEndpointIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public UsersEndpointIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListInactive_ReturnsInactiveUsers_ForManager()
    {
        using var client = _factory.CreateAuthenticatedClient(role: 1);

        using var response = await client.GetAsync("/users/inactive");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Inactive User", document.RootElement[0].GetProperty("displayName").GetString());
    }
}
