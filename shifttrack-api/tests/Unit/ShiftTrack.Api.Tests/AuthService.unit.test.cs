using Microsoft.Extensions.Configuration;
using Moq;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application.Services;
using ShiftTrack.Tests.Shared.Builders;
using ShiftTrack.Tests.Shared.Mocks;
using Xunit;

namespace ShiftTrack.Api.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsRequirePasswordChange_WhenFlagIsSet()
    {
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByEmailAsync("jhon@company.com")).ReturnsAsync(
            new UserBuilder()
                .WithEmail("jhon@company.com")
                .WithDisplayName("Jhon Doe")
                .AsManager()
                .MustChangePassword()
                .WithPasswordHash("hash")
                .Build());
        hasher.Setup(x => x.Verify("Password1!", "hash")).Returns(true);

        var service = CreateService(users, hasher, store);

        var result = await service.LoginAsync("jhon@company.com", "Password1!");

        Assert.False(result.Success);
        Assert.True(result.RequirePasswordChange);
        Assert.Equal("jhon@company.com", result.Email);
    }

    [Fact]
    public async Task LoginAsync_VerifiesDummyHash_WhenUserDoesNotExist()
    {
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByEmailAsync("missing@company.com")).ReturnsAsync((ShiftTrack.Domain.Entities.User?)null);

        var service = CreateService(users, hasher, store);

        var result = await service.LoginAsync("missing@company.com", "Password1!");

        Assert.False(result.Success);
        Assert.Contains("Credentials", result.Message);
        hasher.Verify(x => x.Verify("Password1!", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_PersistsUser_WhenEmailIsAvailable()
    {
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.EmailExistsAsync("new.user@company.com")).ReturnsAsync(false);
        users.Setup(x => x.CreateUserAsync(It.IsAny<ShiftTrack.Domain.Entities.User>())).ReturnsAsync(1);
        hasher.Setup(x => x.Hash("Password1!")).Returns("hashed");

        var service = CreateService(users, hasher, store);

        var result = await service.CreateUserAsync(
            "new.user@company.com",
            "New User",
            1,
            "Password1!",
            "COL",
            "Solvo Global",
            new[] { "Solvo Global" },
            "Leaders",
            "Morning",
            "[]");

        Assert.True(result.Success);
        users.Verify(x => x.CreateUserAsync(It.Is<ShiftTrack.Domain.Entities.User>(user =>
            user.Email == "new.user@company.com" &&
            user.DisplayName == "New User" &&
            user.Role == 1 &&
            user.MustChangePassword &&
            user.ShiftTime == "Morning")), Times.Once);
    }

    [Fact]
    public async Task ForceChangePasswordAsync_ReturnsInvalidToken_WhenTokenCannotBeConsumed()
    {
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByEmailAsync("jhon@company.com")).ReturnsAsync(
            new UserBuilder()
                .WithEmail("jhon@company.com")
                .WithDisplayName("Jhon Doe")
                .AsEmployee()
                .WithPasswordHash("hash")
                .Build());

        var service = CreateService(users, hasher, store);

        var result = await service.ForceChangePasswordAsync("jhon@company.com", "bad-token", "Password1!", isToken: true);

        Assert.False(result.Success);
        Assert.Equal("Invalid or expired token.", result.Message);
    }

    [Fact]
    public async Task LoginWithEntraAsync_CreatesSeedAdmin_WhenConfiguredUserLogsInAndNoActiveUsersExist()
    {
        var objectId = Guid.Parse("90caea51-d682-4d38-82dd-076ad0af5883");
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByObjectIdAsync(objectId)).ReturnsAsync((ShiftTrack.Domain.Entities.User?)null);
        users.Setup(x => x.GetByEmailAsync("admin@solvoglobal.com")).ReturnsAsync((ShiftTrack.Domain.Entities.User?)null);
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<ShiftTrack.Domain.Entities.User>());
        users.Setup(x => x.CreateUserAsync(It.IsAny<ShiftTrack.Domain.Entities.User>())).ReturnsAsync(1);

        var service = CreateService(users, hasher, store, new Dictionary<string, string?>
        {
            ["AzureAd:TenantId"] = "a5ec6523-d52d-443d-bebc-eecf13aae7ac",
            ["SeedAdmin:Email"] = "admin@solvoglobal.com"
        });

        var result = await service.LoginWithEntraAsync(objectId, "Admin@SolvoGlobal.com", "Prod Admin");

        Assert.True(result.Success);
        Assert.Equal(2, result.Role);
        users.Verify(x => x.CreateUserAsync(It.Is<ShiftTrack.Domain.Entities.User>(user =>
            user.Email == "admin@solvoglobal.com" &&
            user.ObjectId == objectId &&
            user.Role == 2 &&
            user.IsActive &&
            user.IsSystemHidden &&
            !user.MustChangePassword)), Times.Once);
    }

    [Fact]
    public async Task LoginWithEntraAsync_HidesExistingSeedAdmin_WhenConfiguredUserLogsIn()
    {
        var objectId = Guid.Parse("90caea51-d682-4d38-82dd-076ad0af5883");
        var existing = new UserBuilder()
            .WithEmail("admin@solvoglobal.com")
            .AsAdmin()
            .Build();
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByObjectIdAsync(objectId)).ReturnsAsync(existing);
        users.Setup(x => x.SetSystemHiddenAsync(existing.Id, true)).ReturnsAsync(1);

        var service = CreateService(users, hasher, store, new Dictionary<string, string?>
        {
            ["SeedAdmin:Email"] = "admin@solvoglobal.com"
        });

        var result = await service.LoginWithEntraAsync(objectId, "admin@solvoglobal.com", "Prod Admin");

        Assert.True(result.Success);
        users.Verify(x => x.SetSystemHiddenAsync(existing.Id, true), Times.Once);
    }

    [Fact]
    public async Task LoginWithEntraAsync_CreatesConfiguredHiddenAdmin_WhenUsersAlreadyExist()
    {
        var objectId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByObjectIdAsync(objectId)).ReturnsAsync((ShiftTrack.Domain.Entities.User?)null);
        users.Setup(x => x.GetByEmailAsync("sara.puerta@solvoglobal.com")).ReturnsAsync((ShiftTrack.Domain.Entities.User?)null);
        users.Setup(x => x.CreateUserAsync(It.IsAny<ShiftTrack.Domain.Entities.User>())).ReturnsAsync(1);

        var service = CreateService(users, hasher, store, new Dictionary<string, string?>
        {
            ["HiddenAdmins:Emails"] = "sara.puerta@solvoglobal.com"
        });

        var result = await service.LoginWithEntraAsync(objectId, "Sara.Puerta@SolvoGlobal.com", "Sara Puerta");

        Assert.True(result.Success);
        Assert.Equal(2, result.Role);
        users.Verify(x => x.GetAllAsync(), Times.Never);
        users.Verify(x => x.CreateUserAsync(It.Is<ShiftTrack.Domain.Entities.User>(user =>
            user.Email == "sara.puerta@solvoglobal.com" &&
            user.ObjectId == objectId &&
            user.Role == 2 &&
            user.IsActive &&
            user.IsSystemHidden &&
            !user.MustChangePassword)), Times.Once);
    }

    [Fact]
    public async Task LoginWithEntraAsync_ElevatesAndHidesExistingConfiguredHiddenAdmin()
    {
        var objectId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var existing = new UserBuilder()
            .WithEmail("sara.puerta@solvoglobal.com")
            .WithDisplayName("Sara Puerta")
            .AsEmployee()
            .Build();
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByObjectIdAsync(objectId)).ReturnsAsync(existing);
        users.Setup(x => x.SetSystemHiddenAsync(existing.Id, true)).ReturnsAsync(1);
        users.Setup(x => x.UpdateUserAsync(
            existing.Id,
            existing.DisplayName!,
            2,
            existing.Location,
            existing.Company,
            existing.CompanyScope,
            existing.Operation,
            existing.ShiftTime,
            existing.ScheduleBlocks)).ReturnsAsync(1);

        var service = CreateService(users, hasher, store, new Dictionary<string, string?>
        {
            ["HiddenAdmins:Emails"] = "sara.puerta@solvoglobal.com"
        });

        var result = await service.LoginWithEntraAsync(objectId, "sara.puerta@solvoglobal.com", "Sara Puerta");

        Assert.True(result.Success);
        Assert.Equal(2, result.Role);
        users.Verify(x => x.SetSystemHiddenAsync(existing.Id, true), Times.Once);
        users.Verify(x => x.UpdateUserAsync(
            existing.Id,
            existing.DisplayName!,
            2,
            existing.Location,
            existing.Company,
            existing.CompanyScope,
            existing.Operation,
            existing.ShiftTime,
            existing.ScheduleBlocks), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ElevatesAndHidesConfiguredHiddenAdmin()
    {
        var existing = new UserBuilder()
            .WithEmail("carlos.colon@solvoglobal.com")
            .WithDisplayName("Carlos Colon")
            .AsEmployee()
            .WithPasswordHash("hash")
            .Build();
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByEmailAsync("carlos.colon@solvoglobal.com")).ReturnsAsync(existing);
        hasher.Setup(x => x.Verify("Password1!", "hash")).Returns(true);
        users.Setup(x => x.SetSystemHiddenAsync(existing.Id, true)).ReturnsAsync(1);
        users.Setup(x => x.UpdateUserAsync(
            existing.Id,
            existing.DisplayName!,
            2,
            existing.Location,
            existing.Company,
            existing.CompanyScope,
            existing.Operation,
            existing.ShiftTime,
            existing.ScheduleBlocks)).ReturnsAsync(1);

        var service = CreateService(users, hasher, store, new Dictionary<string, string?>
        {
            ["HiddenAdmins:Emails"] = "carlos.colon@solvoglobal.com"
        });

        var result = await service.LoginAsync("carlos.colon@solvoglobal.com", "Password1!");

        Assert.True(result.Success);
        Assert.Equal(2, result.Role);
        users.Verify(x => x.SetSystemHiddenAsync(existing.Id, true), Times.Once);
        users.Verify(x => x.UpdateUserAsync(
            existing.Id,
            existing.DisplayName!,
            2,
            existing.Location,
            existing.Company,
            existing.CompanyScope,
            existing.Operation,
            existing.ShiftTime,
            existing.ScheduleBlocks), Times.Once);
    }

    [Fact]
    public async Task LoginWithEntraAsync_DoesNotCreateSeedAdmin_WhenActiveUsersExist()
    {
        var objectId = Guid.Parse("90caea51-d682-4d38-82dd-076ad0af5883");
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var store = new InMemoryResetTokenStore();
        users.Setup(x => x.GetByObjectIdAsync(objectId)).ReturnsAsync((ShiftTrack.Domain.Entities.User?)null);
        users.Setup(x => x.GetByEmailAsync("admin@solvoglobal.com")).ReturnsAsync((ShiftTrack.Domain.Entities.User?)null);
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder().WithEmail("existing@solvoglobal.com").AsAdmin().Build()
        });

        var service = CreateService(users, hasher, store, new Dictionary<string, string?>
        {
            ["SeedAdmin:Email"] = "admin@solvoglobal.com"
        });

        var result = await service.LoginWithEntraAsync(objectId, "admin@solvoglobal.com", "Prod Admin");

        Assert.False(result.Success);
        users.Verify(x => x.CreateUserAsync(It.IsAny<ShiftTrack.Domain.Entities.User>()), Times.Never);
    }

    private static AuthService CreateService(
        Mock<IUserRepository> users,
        Mock<IPasswordHasher> hasher,
        IResetTokenStore tokenStore,
        IDictionary<string, string?>? configValues = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues ?? new Dictionary<string, string?>())
            .Build();

        return new AuthService(users.Object, hasher.Object, tokenStore, config);
    }
}
