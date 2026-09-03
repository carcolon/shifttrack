using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using ShiftTrack.Api;
using ShiftTrack.Application.Interfaces;

namespace ShiftTrack.Api.IntegrationTests.Support;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    internal InMemoryUserRepository Repository { get; } = new();
    internal InMemoryHolidayRepository Holidays { get; } = new();
    internal InMemoryCoverageRuleRepository CoverageRules { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<ICoverageRuleRepository>();
            services.RemoveAll<IHolidayRepository>();
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IEmailService>();
            services.RemoveAll<IHubContext<ScheduleHub>>();

            services.AddSingleton(Repository);
            services.AddSingleton(Holidays);
            services.AddSingleton(CoverageRules);
            services.AddSingleton<IUserRepository>(Repository);
            services.AddSingleton<ICoverageRuleRepository>(CoverageRules);
            services.AddSingleton<IHolidayRepository>(Holidays);
            services.AddSingleton<IAuthService>(new FakeAuthService(Repository));
            services.AddSingleton<IEmailService, NoOpEmailService>();
            var clients = new Mock<IHubClients>();
            clients.Setup(x => x.All).Returns(Mock.Of<IClientProxy>());
            var hub = new Mock<IHubContext<ScheduleHub>>();
            hub.Setup(x => x.Clients).Returns(clients.Object);
            services.AddSingleton<IHubContext<ScheduleHub>>(hub.Object);
        });
    }

    public HttpClient CreateAuthenticatedClient(int role = 1, string? email = null, string? name = null, bool isSystemHidden = false, string? company = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var principalEmail = email ?? "manager@company.com";
        var principalName = name ?? "Test User";
        var principalUser = new ShiftTrack.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ObjectId = Guid.NewGuid(),
            Email = principalEmail,
            DisplayName = principalName,
            Role = role,
            IsActive = true,
            PasswordHash = "hash",
            MustChangePassword = false,
            CreatedAtUtc = DateTime.UtcNow,
            Location = "COL",
            Company = company ?? "Solvo Global",
            Operation = "Leaders",
            ShiftTime = "Morning",
            IsSystemHidden = isSystemHidden
        };
        if (Repository.GetByEmailAsync(principalEmail).GetAwaiter().GetResult() is null)
        {
            Repository.CreateUserAsync(principalUser).GetAwaiter().GetResult();
        }
        var token = AuthHelpers.CreateAccessToken(
            principalUser,
            "dev-only-shifttrack-signing-key-change-before-production",
            "ShiftTrack",
            "ShiftTrack.Client",
            TimeSpan.FromMinutes(30));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
