using System.Net.Http.Json;
using System.Text.Json;
using ShiftTrack.Api.IntegrationTests.Support;
using Xunit;

namespace ShiftTrack.Api.IntegrationTests;

public sealed class AuthEndpointIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthEndpointIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Me_DoesNotReturnJwtInResponseBody()
    {
        using var client = _factory.CreateAuthenticatedClient(role: 1, email: "jhon.doe@company.com");

        using var response = await client.GetAsync("/auth/me");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.TryGetProperty("token", out _));
    }

    [Fact]
    public void AuthResponse_DoesNotSerializeJwtTokenProperty()
    {
        var response = new AuthResponse(
            "jhon.doe@company.com",
            "Jhon Doe",
            1,
            new[] { "viewSchedule" },
            false,
            "Solvo Global",
            new[] { "Solvo Global" });

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));

        Assert.False(document.RootElement.TryGetProperty("token", out _));
        Assert.False(document.RootElement.TryGetProperty("Token", out _));
    }
}
