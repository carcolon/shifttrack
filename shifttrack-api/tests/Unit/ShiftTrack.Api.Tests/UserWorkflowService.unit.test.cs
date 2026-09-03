using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Moq;
using ShiftTrack.Api;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application.Models;
using ShiftTrack.Domain.Entities;
using ShiftTrack.Tests.Shared.Builders;
using ShiftTrack.Tests.Shared.Mocks;
using Xunit;

namespace ShiftTrack.Api.Tests;

public class UserWorkflowServiceTests
{
    [Fact]
    public async Task CreateUserAsync_SendsWelcomeEmail_WhenSystemHiddenAdminCreatesAdmin()
    {
        var email = new FakeEmailService();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("ShiftTrack")
            .Build());

        var service = CreateService(email: email, users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.CreateUserAsync(context, new CreateUserRequest(
            "New",
            "Admin",
            "admin@company.com",
            "Temp123!",
            2,
            "Bogota",
            "ShiftTrack",
            "Admin",
            new[]
            {
                new SchedulePeriodRequest(
                    "2026-03-14",
                    null,
                    "Morning",
                    new[] { new ScheduleBlockRequest("08:00", "17:00", new[] { "Mon" }) })
            },
            new[] { "ShiftTrack" }));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        var welcome = Assert.Single(email.Sent, message => message.Kind == "welcome");
        Assert.Equal("admin@company.com", Assert.Single(welcome.Recipients));
        Assert.Equal("Temp123!", welcome.Payload["tempPassword"]);
        Assert.Contains("/reset?code=", Assert.IsType<string>(welcome.Payload["resetLink"]));
    }

    [Fact]
    public async Task CreateUserAsync_ReturnsForbidden_WhenManagerCreatesAdmin()
    {
        var service = CreateService();
        var context = BuildHttpContext(role: 1, email: "manager@company.com");

        var result = await service.CreateUserAsync(context, new CreateUserRequest(
            "A",
            "B",
            "new@company.com",
            "Temp123!",
            2,
            "Bogota",
            "ShiftTrack",
            "Ops",
            new[]
            {
                new SchedulePeriodRequest(
                    "2026-03-14",
                    null,
                    "Morning",
                    new[] { new ScheduleBlockRequest("08:00", "17:00", new[] { "Mon" }) })
            }));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUserAsync_MarksUserSystemHidden_WhenSystemHiddenAdminCreatesSuperAdmin()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetByEmailAsync("new.super@company.com")).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("new.super@company.com")
            .AsAdmin()
            .Build());
        users.Setup(x => x.SetSystemHiddenAsync(targetId, true)).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.CreateUserAsync(context, new CreateUserRequest(
            "New",
            "Super",
            "new.super@company.com",
            "Temp123!",
            2,
            "Bogota",
            "ShiftTrack",
            "Admin",
            new[]
            {
                new SchedulePeriodRequest(
                    "2026-03-14",
                    null,
                    "Morning",
                    new[] { new ScheduleBlockRequest("08:00", "17:00", new[] { "Mon" }) })
            },
            new[] { "ShiftTrack" },
            true));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.SetSystemHiddenAsync(targetId, true), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_AllowsSystemHiddenAdminWithoutCompanyOrSchedule()
    {
        var targetId = Guid.NewGuid();
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.CreateUserAsync(
                "new.super@company.com",
                "New Super",
                2,
                "Temp123!",
                "Bogota",
                "",
                It.Is<IEnumerable<string>?>(companies => companies != null && !companies.Any()),
                "Admin",
                "",
                null))
            .ReturnsAsync(new AuthResult(true, null, null, null, null, false));
        auth.Setup(x => x.GenerateResetTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync("token");

        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetByEmailAsync("new.super@company.com")).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("new.super@company.com")
            .AsAdmin()
            .WithCompany("")
            .WithShiftTime("")
            .WithScheduleBlocks(null)
            .Build());
        users.Setup(x => x.SetSystemHiddenAsync(targetId, true)).ReturnsAsync(1);

        var service = CreateService(auth: auth, users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.CreateUserAsync(context, new CreateUserRequest(
            "New",
            "Super",
            "new.super@company.com",
            "Temp123!",
            2,
            "Bogota",
            "",
            "Admin",
            Array.Empty<SchedulePeriodRequest>(),
            Array.Empty<string>(),
            true));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.ReplaceUserSchedulePeriodsAsync(targetId, It.Is<IEnumerable<UserSchedulePeriod>>(periods => !periods.Any()), "", null), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_ReturnsForbidden_WhenRegularAdminCreatesSuperAdmin()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("ShiftTrack")
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");

        var result = await service.CreateUserAsync(context, new CreateUserRequest(
            "New",
            "Super",
            "new.super@company.com",
            "Temp123!",
            2,
            "Bogota",
            "ShiftTrack",
            "Admin",
            new[]
            {
                new SchedulePeriodRequest(
                    "2026-03-14",
                    null,
                    "Morning",
                    new[] { new ScheduleBlockRequest("08:00", "17:00", new[] { "Mon" }) })
            },
            new[] { "ShiftTrack" },
            true));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListSystemHiddenUsersAsync_ReturnsOnlyForSystemHiddenAdmin()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetSystemHiddenAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithEmail("super@company.com")
                .WithDisplayName("Super Admin")
                .AsAdmin()
                .SystemHidden()
                .Build()
        });

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.ListSystemHiddenUsersAsync(context);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("super@company.com", response.Body);
    }

    [Fact]
    public async Task ListUsersAsync_ReturnsOk_WithSchedulePeriods_ForManager()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee One")
                .AsEmployee()
                .WithOperation("Ops")
                .WithLocation("Bogota")
                .WithCompany("ShiftTrack")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\"]}]")
                .WithSchedulePeriods(
                    new UserSchedulePeriodBuilder()
                        .WithUserId(Guid.NewGuid())
                        .EffectiveFrom(new DateTime(2026, 3, 14))
                        .WithShiftTime("Morning")
                        .WithBlocksJson("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\"]}]")
                        .Build())
                .Build()
        });

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");

        var result = await service.ListUsersAsync(context, inactiveOnly: false);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("employee@company.com", response.Body);
        Assert.Contains("08:00", response.Body);
    }

    [Fact]
    public async Task ListUsersAsync_FiltersActiveAndInactiveUsersByCallerCompanyScope()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithEmail("employee@company.com")
                .AsEmployee()
                .WithCompany("ShiftTrack")
                .Build(),
            new UserBuilder()
                .WithEmail("external.active@otherco.com")
                .AsEmployee()
                .WithCompany("OtherCo")
                .Build()
        });
        users.Setup(x => x.GetInactiveAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithEmail("inactive@company.com")
                .AsEmployee()
                .WithCompany("ShiftTrack")
                .Inactive()
                .Build(),
            new UserBuilder()
                .WithEmail("external.inactive@otherco.com")
                .AsEmployee()
                .WithCompany("OtherCo")
                .Inactive()
                .Build()
        });

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");

        var activeResult = await service.ListUsersAsync(context, inactiveOnly: false);
        var inactiveResult = await service.ListUsersAsync(context, inactiveOnly: true);

        var activeResponse = await ResultTestHelpers.ExecuteAsync(activeResult);
        var inactiveResponse = await ResultTestHelpers.ExecuteAsync(inactiveResult);
        Assert.Equal(StatusCodes.Status200OK, activeResponse.StatusCode);
        Assert.Contains("employee@company.com", activeResponse.Body);
        Assert.DoesNotContain("external.active@otherco.com", activeResponse.Body);
        Assert.Equal(StatusCodes.Status200OK, inactiveResponse.StatusCode);
        Assert.Contains("inactive@company.com", inactiveResponse.Body);
        Assert.DoesNotContain("external.inactive@otherco.com", inactiveResponse.Body);
    }

    [Fact]
    public async Task ListUsersAsync_ReturnsForbidden_ForEmployee()
    {
        var service = CreateService();
        var context = BuildHttpContext(role: 0, email: "employee@company.com");

        var result = await service.ListUsersAsync(context, inactiveOnly: false);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserAsync_AllowsManagerUpdatingEmployee_WhenIsSystemHiddenFalseIsPresent()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@company.com")
            .AsEmployee()
            .WithCompany("ShiftTrack")
            .Active()
            .Build());
        users.Setup(x => x.UpdateUserAsync(
                targetId,
                "Employee One",
                0,
                "Bogota",
                "ShiftTrack",
                It.IsAny<string?>(),
                "Ops",
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");

        var result = await service.UpdateUserAsync(context, targetId, new UpdateUserRequest(
            "Employee",
            "One",
            0,
            "Bogota",
            "ShiftTrack",
            "Ops",
            new[]
            {
                new SchedulePeriodRequest(
                    "2026-03-14",
                    null,
                    "Morning",
                    new[] { new ScheduleBlockRequest("08:00", "17:00", new[] { "Mon" }) })
            },
            new[] { "ShiftTrack" },
            false));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.SetSystemHiddenAsync(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
        users.Verify(x => x.UpdateUserAsync(
            targetId,
            "Employee One",
            0,
            "Bogota",
            "ShiftTrack",
            It.IsAny<string?>(),
            "Ops",
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_AllowsSystemHiddenAdminWithoutCompanyOrSchedule()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("target.super@company.com")
            .AsAdmin()
            .SystemHidden()
            .WithCompany("")
            .WithShiftTime("")
            .WithScheduleBlocks(null)
            .Active()
            .Build());
        users.Setup(x => x.UpdateUserAsync(
                targetId,
                "Target Super",
                2,
                "Bogota",
                "",
                "[]",
                "Admin",
                "",
                null))
            .ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.UpdateUserAsync(context, targetId, new UpdateUserRequest(
            "Target",
            "Super",
            2,
            "Bogota",
            "",
            "Admin",
            Array.Empty<SchedulePeriodRequest>(),
            Array.Empty<string>(),
            true));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.ReplaceUserSchedulePeriodsAsync(targetId, It.Is<IEnumerable<UserSchedulePeriod>>(periods => !periods.Any()), "", null), Times.Once);
    }

    [Fact]
    public async Task PurgeUserAsync_HardDeletesInactiveEmployeeInCompanyScope_ForManagerWithConfirmation()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@company.com")
            .AsEmployee()
            .WithCompany("ShiftTrack")
            .Inactive()
            .Build());
        users.Setup(x => x.HardDeleteAsync(targetId)).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.Headers["X-Purge-Confirm"] = "PURGE";

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.HardDeleteAsync(targetId), Times.Once);
    }

    [Fact]
    public async Task PurgeUserAsync_ReturnsBadRequest_WhenConfirmationMissing()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("confirmation", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PurgeUserAsync_ReturnsBadRequest_WhenTargetIsActive()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("target@company.com")
            .AsEmployee()
            .Active()
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");
        context.Request.Headers["X-Purge-Confirm"] = "PURGE";

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("inactive", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PurgeUserAsync_HardDeletesInactiveUser_ForSystemHiddenAdminWithConfirmation()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("target@company.com")
            .AsEmployee()
            .Inactive()
            .Build());
        users.Setup(x => x.HardDeleteAsync(targetId)).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");
        context.Request.Headers["X-Purge-Confirm"] = "PURGE";

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.HardDeleteAsync(targetId), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsBadRequest_WhenSuperAdminDeletesSelf()
    {
        var selfId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var users = new Mock<IUserRepository>();
        var self = new UserBuilder()
            .WithId(selfId)
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(self);
        users.Setup(x => x.GetByIdAsync(selfId)).ReturnsAsync(self);
        users.Setup(x => x.CountActiveSystemHiddenAsync()).ReturnsAsync(2);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.DeleteUserAsync(context, selfId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("cannot", response.Body, StringComparison.OrdinalIgnoreCase);
        users.Verify(x => x.SoftDeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsBadRequest_WhenDeletingLastActiveSuperAdmin()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("super@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("other.super@company.com")
            .AsAdmin()
            .SystemHidden()
            .Build());
        users.Setup(x => x.CountActiveSystemHiddenAsync()).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "super@company.com");

        var result = await service.DeleteUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("At least one active super admin", response.Body);
        users.Verify(x => x.SoftDeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PurgeUserAsync_HardDeletesInactiveEmployeeInCompanyScope_ForAdminWithConfirmation()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@company.com")
            .AsEmployee()
            .WithCompany("ShiftTrack")
            .Inactive()
            .Build());
        users.Setup(x => x.HardDeleteAsync(targetId)).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");
        context.Request.Headers["X-Purge-Confirm"] = "PURGE";

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.HardDeleteAsync(targetId), Times.Once);
    }

    [Fact]
    public async Task PurgeUserAsync_ReturnsForbidden_ForAdminOutsideCompanyScope()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@other.com")
            .AsEmployee()
            .WithCompany("OtherCo")
            .Inactive()
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");
        context.Request.Headers["X-Purge-Confirm"] = "PURGE";

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        users.Verify(x => x.HardDeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PurgeUserAsync_HardDeletesInactiveManagerInCompanyScope_ForAdminWithConfirmation()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Inactive()
            .Build());
        users.Setup(x => x.HardDeleteAsync(targetId)).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");
        context.Request.Headers["X-Purge-Confirm"] = "PURGE";

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.HardDeleteAsync(targetId), Times.Once);
    }

    [Fact]
    public async Task PurgeUserAsync_ReturnsForbidden_ForManagerPurgingManager()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("other.manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Inactive()
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.Headers["X-Purge-Confirm"] = "PURGE";

        var result = await service.PurgeUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        users.Verify(x => x.HardDeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_InactivatesManagerInCompanyScope_ForAdmin()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Active()
            .Build());
        users.Setup(x => x.SoftDeleteAsync(targetId)).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");

        var result = await service.DeleteUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.SoftDeleteAsync(targetId), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_InactivatesTeamLeaderInCompanyScope_ForManager()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("lead@company.com")
            .WithRole(3)
            .WithCompany("ShiftTrack")
            .Active()
            .Build());
        users.Setup(x => x.SoftDeleteAsync(targetId)).ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");

        var result = await service.DeleteUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        users.Verify(x => x.SoftDeleteAsync(targetId), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsForbidden_ForManagerInactivatingManager()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("other.manager@company.com")
            .AsManager()
            .WithCompany("ShiftTrack")
            .Active()
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");

        var result = await service.DeleteUserAsync(context, targetId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        users.Verify(x => x.SoftDeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    private static UserWorkflowService CreateService(
        Mock<IAuthService>? auth = null,
        FakeEmailService? email = null,
        Mock<IPasswordHasher>? hasher = null,
        Mock<IUserRepository>? users = null,
        Mock<IHubContext<ScheduleHub>>? hub = null)
    {
        auth ??= new Mock<IAuthService>();
        auth.Setup(x => x.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuthResult(true, null, null, null, null, false));
        auth.Setup(x => x.GenerateResetTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync("token");

        email ??= new FakeEmailService();
        hasher ??= new Mock<IPasswordHasher>();
        hasher.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashed");
        users ??= new Mock<IUserRepository>();
        hub ??= CreateHub();

        return new UserWorkflowService(
            auth.Object,
            email,
            hasher.Object,
            users.Object,
            hub.Object,
            new StartupOptions { FrontendBaseUrl = "https://frontend.local" });
    }

    private static Mock<IHubContext<ScheduleHub>> CreateHub()
    {
        var clients = new Mock<IHubClients>();
        clients.Setup(x => x.All).Returns(new SpyHubClientProxy());
        var hub = new Mock<IHubContext<ScheduleHub>>();
        hub.Setup(x => x.Clients).Returns(clients.Object);
        return hub;
    }

    private static HttpContext BuildHttpContext(int role, string email)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("role", role.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "Test");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }
}
