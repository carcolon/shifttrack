using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ShiftTrack.Api.IntegrationTests.Support;
using Xunit;

namespace ShiftTrack.Api.AcceptanceTests;

public sealed class UserManagementAcceptanceTests
{
    [Fact]
    public async Task ManagerCanCreateEmployee_ThenSeeItInUsersList()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient(role: 1, email: "manager@company.com");
        var email = $"acceptance.employee.{Guid.NewGuid():N}@company.com";

        using var createResponse = await client.PostAsJsonAsync("/users", BuildCreateUserRequest(email, role: 0, operation: "Leaders"));
        createResponse.EnsureSuccessStatusCode();

        using var listResponse = await client.GetAsync("/users");
        listResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());

        Assert.Contains(document.RootElement.EnumerateArray(), item =>
            string.Equals(item.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdminCanCreateUser_ThenTrackyAndExportReflectTheNewUser()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient(role: 2, email: "admin@company.com");
        var email = $"acceptance.admin.{Guid.NewGuid():N}@company.com";

        using var createResponse = await client.PostAsJsonAsync("/users", BuildCreateUserRequest(email, role: 2, operation: "Leaders"));
        createResponse.EnsureSuccessStatusCode();

        using var assistantResponse = await client.PostAsJsonAsync("/assistant/query", new
        {
            message = "who are admins?",
            weekStart = "2026-03-09"
        });
        assistantResponse.EnsureSuccessStatusCode();
        using var assistantDocument = JsonDocument.Parse(await assistantResponse.Content.ReadAsStringAsync());
        Assert.Contains(assistantDocument.RootElement.GetProperty("matches").EnumerateArray(), item =>
            string.Equals(item.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase));

        using var exportResponse = await client.GetAsync($"/calendar/export?weekStart=2026-03-09&employee={Uri.EscapeDataString(email)}");
        exportResponse.EnsureSuccessStatusCode();
        await using var stream = new MemoryStream(await exportResponse.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var firstDataRowName = worksheet.Cell(2, 1).GetString();

        Assert.Equal("Acceptance Admin", firstDataRowName);
    }

    private static object BuildCreateUserRequest(string email, int role, string operation) => new
    {
        firstName = "Acceptance",
        lastName = role == 2 ? "Admin" : "Employee",
        email,
        password = "Password1!",
        role,
        location = "COL",
        company = "Solvo Global",
        operation,
        schedulePeriods = new[]
        {
            new
            {
                effectiveFrom = "2026-03-01",
                effectiveTo = (string?)null,
                shiftTime = "Morning",
                scheduleBlocks = new[]
                {
                    new
                    {
                        start = "08:00",
                        end = "17:00",
                        days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" }
                    }
                }
            }
        }
    };
}
