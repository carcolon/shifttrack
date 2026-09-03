using System.Net;
using System.Net.Http.Json;
using ShiftTrack.Api;
using ShiftTrack.Api.IntegrationTests.Support;
using Xunit;

namespace ShiftTrack.Api.IntegrationTests;

public sealed class CompanyEndpointsIntegrationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public CompanyEndpointsIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCompanyOperations_ReturnsOnlyCallerCompanyScope()
    {
        using var esquireClient = _factory.CreateAuthenticatedClient(role: 0, email: "employee.esquire@example.com", company: "Esquire Law, LLC");
        using var solvoClient = _factory.CreateAuthenticatedClient(role: 0, email: "employee.solvo@example.com", company: "Solvo Global");

        var esquireOperations = await esquireClient.GetFromJsonAsync<List<CompanyOperationResponse>>("/companies/operations");
        var solvoOperations = await solvoClient.GetFromJsonAsync<List<CompanyOperationResponse>>("/companies/operations");

        Assert.Contains(esquireOperations!, operation => operation.CompanyName == "Esquire Law, LLC" && operation.Name == "Referral");
        Assert.DoesNotContain(solvoOperations!, operation => operation.CompanyName == "Esquire Law, LLC");
        Assert.DoesNotContain(solvoOperations!, operation => operation.Name == "Referral" && operation.CompanyName == "Esquire Law, LLC");
    }

    [Fact]
    public async Task CompanyOperationMutations_RequireSystemHiddenUser()
    {
        using var managerClient = _factory.CreateAuthenticatedClient(role: 1, email: "manager.operations@example.com");
        using var superAdminClient = _factory.CreateAuthenticatedClient(role: 2, email: "super.operations@example.com", isSystemHidden: true);

        using var forbidden = await managerClient.PostAsJsonAsync("/companies/operations", new
        {
            companyName = "Solvo Global",
            name = "Intake"
        });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var created = await superAdminClient.PostAsJsonAsync("/companies/operations", new
        {
            companyName = "Solvo Global",
            name = "Intake"
        });

        created.EnsureSuccessStatusCode();
        var operations = await superAdminClient.GetFromJsonAsync<List<CompanyOperationResponse>>("/companies/operations?company=Solvo%20Global&includeInactive=true");
        Assert.Contains(operations!, operation => operation.CompanyName == "Solvo Global" && operation.Name == "Intake");
    }
}
