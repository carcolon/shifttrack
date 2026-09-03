using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Moq;
using ShiftTrack.Api;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;
using ShiftTrack.Tests.Shared.Builders;
using ShiftTrack.Tests.Shared.Mocks;
using Xunit;

namespace ShiftTrack.Api.Tests;

public class ScheduleWorkflowServiceTests
{
    [Fact]
    public async Task PtoReviewAuthorization_AllowsManagerToReviewEmployeeRequestCreatedByManager()
    {
        var requirement = new PtoReviewRequirement();
        var handler = new PtoReviewAuthorizationHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, RoleHelpers.Manager.ToString())
        }, "Test"));
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            new PtoReviewResource(RoleHelpers.Employee, RoleHelpers.Manager));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task PtoReviewAuthorization_DeniesManagerReviewForAdminTarget()
    {
        var requirement = new PtoReviewRequirement();
        var handler = new PtoReviewAuthorizationHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, RoleHelpers.Manager.ToString())
        }, "Test"));
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            new PtoReviewResource(RoleHelpers.Admin, RoleHelpers.Employee));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_ReturnsBadRequest_ForUnknownRequestType()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(
            new UserBuilder()
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee")
                .AsEmployee()
                .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: "employee@company.com");
        var request = new UpsertPtoRequest(Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd"), 1, "weird_type", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Request Type is required", response.Body);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_ReturnsBadRequest_ForEmployeePastDate()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(
            new UserBuilder()
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee")
                .AsEmployee()
                .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: "employee@company.com");
        var request = new UpsertPtoRequest(Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd"), 1, "Vacation", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("past dates", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_ReturnsBadRequest_ForEmployeeBeyondSixtyDays()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(
            new UserBuilder()
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee")
                .AsEmployee()
                .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: "employee@company.com");
        var request = new UpsertPtoRequest(Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(61).ToString("yyyy-MM-dd"), 1, "Vacation", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("60 days", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_ReturnsBadRequest_ForDuplicateDateRequest()
    {
        var users = new Mock<IUserRepository>();
        var userId = Guid.NewGuid();
        users.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(
            new UserBuilder()
                .WithId(userId)
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee")
                .AsEmployee()
                .Build());
        users.Setup(x => x.GetByEmailAsync("employee@company.com")).ReturnsAsync(
            new UserBuilder()
                .WithId(userId)
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee")
                .AsEmployee()
                .Build());
        users.Setup(x => x.GetOverlappingActivePtoRequestAsync(
                userId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new PtoRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RequestType = "day_off",
                Status = "pending",
                StartDate = DateTime.UtcNow.Date.AddDays(2),
                EndDate = DateTime.UtcNow.Date.AddDays(2)
            });

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: "employee@company.com");
        var request = new UpsertPtoRequest(userId, DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd"), 1, "vacations", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("A request has already been detected for this date.", response.Body);
        Assert.Contains("Please review your existing request before submitting a new one.", response.Body);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_ExpandsVacationAcrossWeekend()
    {
        var capturedRequest = default(PtoRequest);
        var users = new Mock<IUserRepository>();
        var userId = Guid.NewGuid();
        users.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(
            new UserBuilder()
                .WithId(userId)
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee")
                .AsEmployee()
                .Build());
        users.Setup(x => x.GetByEmailAsync("employee@company.com")).ReturnsAsync(
            new UserBuilder()
                .WithId(userId)
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee")
                .AsEmployee()
                .Build());
        users.Setup(x => x.UpsertPtoRequestAsync(It.IsAny<PtoRequest>()))
            .Callback<PtoRequest>(request => capturedRequest = request)
            .ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: "employee@company.com");
        var startDate = NextWeekday(DateTime.UtcNow.Date, DayOfWeek.Friday);
        var expectedEndDate = startDate.AddDays(3);
        var request = new UpsertPtoRequest(userId, startDate.ToString("yyyy-MM-dd"), 2, "vacations", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(capturedRequest);
        Assert.Equal(startDate, capturedRequest!.StartDate);
        Assert.Equal(expectedEndDate, capturedRequest.EndDate);
        Assert.Equal(2, capturedRequest.NumberOfDays);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_DoesNotExpandVacationAcrossWeekend_ForNonColombianLocation()
    {
        var capturedRequest = default(PtoRequest);
        var users = new Mock<IUserRepository>();
        var holidays = new Mock<IHolidayRepository>();
        var userId = Guid.NewGuid();
        var target = new UserBuilder()
            .WithId(userId)
            .WithEmail("employee@company.com")
            .WithDisplayName("Employee")
            .AsEmployee()
            .WithLocation("United States")
            .Build();
        users.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(target);
        users.Setup(x => x.GetByEmailAsync("employee@company.com")).ReturnsAsync(target);
        users.Setup(x => x.UpsertPtoRequestAsync(It.IsAny<PtoRequest>()))
            .Callback<PtoRequest>(request => capturedRequest = request)
            .ReturnsAsync(1);

        var service = CreateService(users: users, holidays: holidays);
        var context = BuildHttpContext(role: 0, email: "employee@company.com");
        var startDate = NextWeekday(DateTime.UtcNow.Date, DayOfWeek.Friday);
        var request = new UpsertPtoRequest(userId, startDate.ToString("yyyy-MM-dd"), 2, "vacations", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(capturedRequest);
        Assert.Equal(startDate, capturedRequest!.StartDate);
        Assert.Equal(startDate.AddDays(1), capturedRequest.EndDate);
        holidays.Verify(x => x.GetActiveInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_SendsApprovalEmailOnlyToReviewersInTargetCompanyScope()
    {
        var targetId = Guid.NewGuid();
        var target = new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@solvo.com")
            .WithDisplayName("Employee")
            .AsEmployee()
            .WithCompany("Solvo Global")
            .Build();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(target);
        users.Setup(x => x.GetByEmailAsync("employee@solvo.com")).ReturnsAsync(target);
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("manager@solvo.com").AsManager().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("admin@solvo.com").AsAdmin().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("admin@other.com").AsAdmin().WithCompany("OtherCo").Build()
        });
        users.Setup(x => x.UpsertPtoRequestAsync(It.IsAny<PtoRequest>())).ReturnsAsync(1);

        var email = new FakeEmailService();
        var service = CreateService(users: users, email: email);
        var context = BuildHttpContext(role: 0, email: "employee@solvo.com");
        var request = new UpsertPtoRequest(targetId, DateTime.UtcNow.Date.AddDays(5).ToString("yyyy-MM-dd"), 1, "vacations", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        var approval = Assert.Single(email.Sent, item => item.Kind == "pto-approval");
        Assert.Contains("manager@solvo.com", approval.Recipients);
        Assert.Contains("admin@solvo.com", approval.Recipients);
        Assert.DoesNotContain("admin@other.com", approval.Recipients);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_WhenManagerCreatesPendingRequest_SendsApprovalEmailToManagersAndAdminsIncludingCreator()
    {
        var targetId = Guid.NewGuid();
        var manager = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithEmail("creator.manager@solvo.com")
            .WithDisplayName("Creator Manager")
            .AsManager()
            .WithCompany("Solvo Global")
            .Build();
        var target = new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@solvo.com")
            .WithDisplayName("Employee")
            .AsEmployee()
            .WithCompany("Solvo Global")
            .Build();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(target);
        users.Setup(x => x.GetByEmailAsync(manager.Email)).ReturnsAsync(manager);
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            manager,
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("peer.manager@solvo.com").AsManager().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("admin@solvo.com").AsAdmin().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("manager@other.com").AsManager().WithCompany("OtherCo").Build()
        });
        users.Setup(x => x.UpsertPtoRequestAsync(It.IsAny<PtoRequest>())).ReturnsAsync(1);

        var email = new FakeEmailService();
        var service = CreateService(users: users, email: email);
        var context = BuildHttpContext(role: RoleHelpers.Manager, email: manager.Email);
        var request = new UpsertPtoRequest(targetId, DateTime.UtcNow.Date.AddDays(5).ToString("yyyy-MM-dd"), 1, "vacations", "Coverage requested", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        var approval = Assert.Single(email.Sent, item => item.Kind == "pto-approval");
        Assert.Contains("creator.manager@solvo.com", approval.Recipients);
        Assert.Contains("peer.manager@solvo.com", approval.Recipients);
        Assert.Contains("admin@solvo.com", approval.Recipients);
        Assert.DoesNotContain("manager@other.com", approval.Recipients);
    }

    [Fact]
    public async Task PreviewCalendarPtoCoverageAsync_UsesGlobalCoverage_WhenNoFiltersAreSelected()
    {
        var targetId = Guid.NewGuid();
        var target = new UserBuilder()
            .WithId(targetId)
            .WithEmail("admin@solvo.com")
            .WithDisplayName("Admin")
            .AsAdmin()
            .WithCompany("Solvo Global")
            .WithOperation("Leaders")
            .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
            .Build();
        var users = BuildCoveragePreviewUsers(targetId, target);
        var coverageRules = BuildCoverageRules(expectedCoverage: 60);

        var service = CreateService(users: users, coverageRules: coverageRules);
        var context = BuildHttpContext(role: RoleHelpers.Admin, email: target.Email);
        var startDate = NextWeekday(DateTime.UtcNow.Date, DayOfWeek.Monday);
        var request = new UpsertPtoRequest(targetId, startDate.ToString("yyyy-MM-dd"), 1, "vacations", "Test reason", null);

        var result = await service.PreviewCalendarPtoCoverageAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("\"hasImpact\":false", response.Body);
        Assert.Contains("\"warnings\":[]", response.Body);
    }

    [Fact]
    public async Task PreviewCalendarPtoCoverageAsync_UsesFilteredCoverage_WhenFiltersAreSelected()
    {
        var targetId = Guid.NewGuid();
        var target = new UserBuilder()
            .WithId(targetId)
            .WithEmail("admin@solvo.com")
            .WithDisplayName("Admin")
            .AsAdmin()
            .WithCompany("Solvo Global")
            .WithOperation("Leaders")
            .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
            .Build();
        var users = BuildCoveragePreviewUsers(targetId, target);
        var coverageRules = BuildCoverageRules(expectedCoverage: 60);

        var service = CreateService(users: users, coverageRules: coverageRules);
        var context = BuildHttpContext(role: RoleHelpers.Admin, email: target.Email);
        var startDate = NextWeekday(DateTime.UtcNow.Date, DayOfWeek.Monday);
        var request = new UpsertPtoRequest(
            targetId,
            startDate.ToString("yyyy-MM-dd"),
            1,
            "vacations",
            "Test reason",
            null,
            OperationFilter: "Leaders");

        var result = await service.PreviewCalendarPtoCoverageAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("\"hasImpact\":true", response.Body);
        Assert.Contains("\"currentWorkingAgents\":2", response.Body);
        Assert.Contains("\"projectedWorkingAgents\":1", response.Body);
    }

    [Fact]
    public async Task PreviewCalendarPtoCoverageAsync_DoesNotExpandToCompanyScope_WhenFiltersAreSelected()
    {
        var targetId = Guid.NewGuid();
        var target = new UserBuilder()
            .WithId(targetId)
            .WithEmail("admin@solvo.com")
            .WithDisplayName("Admin")
            .AsAdmin()
            .WithCompany("Peter's Pan")
            .WithOperation("Leaders")
            .WithShiftTime("Morning")
            .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
            .Build();
        var users = BuildCoveragePreviewUsers(targetId, target, companyName: "Peter's Pan", otherOperationShift: "Late");
        var coverageRules = BuildCoverageRules(expectedCoverage: 85, calculationScope: "company");

        var service = CreateService(users: users, coverageRules: coverageRules);
        var context = BuildHttpContext(role: RoleHelpers.Admin, email: target.Email);
        var startDate = NextWeekday(DateTime.UtcNow.Date, DayOfWeek.Tuesday);
        var request = new UpsertPtoRequest(
            targetId,
            startDate.ToString("yyyy-MM-dd"),
            1,
            "vacations",
            "Test reason",
            null,
            CompanyFilter: "Peter's Pan",
            ShiftFilter: "Morning");

        var result = await service.PreviewCalendarPtoCoverageAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("\"hasImpact\":true", response.Body);
        Assert.Contains("\"requiredAgents\":2", response.Body);
        Assert.Contains("\"currentWorkingAgents\":2", response.Body);
        Assert.Contains("\"projectedWorkingAgents\":1", response.Body);
        Assert.Contains("50", response.Body);
        Assert.DoesNotContain("\"requiredAgents\":6", response.Body);
        Assert.DoesNotContain("16.7", response.Body);
    }

    [Fact]
    public async Task GetPtoRequestsAsync_ReturnsAllOwnRequests_ForEmployee()
    {
        var ownEmail = "employee@company.com";
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetPtoRequestsAsync("pending", It.IsAny<int>()))
            .ReturnsAsync(new[]
            {
                new PtoRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    UserEmail = ownEmail,
                    UserDisplayName = "Employee",
                    RequestType = "day_off",
                    NumberOfDays = 1,
                    StartDate = DateTime.UtcNow.Date.AddDays(1),
                    EndDate = DateTime.UtcNow.Date.AddDays(1),
                    Status = "pending",
                    RequestedByEmail = ownEmail,
                    RequestedByName = "Employee",
                    RequestedByRole = 0,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new PtoRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    UserEmail = "other@company.com",
                    UserDisplayName = "Other",
                    RequestType = "day_off",
                    NumberOfDays = 1,
                    StartDate = DateTime.UtcNow.Date.AddDays(1),
                    EndDate = DateTime.UtcNow.Date.AddDays(1),
                    Status = "pending",
                    RequestedByEmail = "other@company.com",
                    RequestedByName = "Other",
                    RequestedByRole = 0,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new PtoRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    UserEmail = ownEmail,
                    UserDisplayName = "Employee",
                    RequestType = "vacations",
                    NumberOfDays = 1,
                    StartDate = DateTime.UtcNow.Date.AddDays(2),
                    EndDate = DateTime.UtcNow.Date.AddDays(2),
                    Status = "pending",
                    RequestedByEmail = ownEmail,
                    RequestedByName = "Employee",
                    RequestedByRole = 0,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            });

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: ownEmail);
        context.Request.QueryString = new QueryString("?status=pending&take=300");

        var result = await service.GetPtoRequestsAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("\"requestType\":\"day_off\"", response.Body);
        Assert.Contains(ownEmail, response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("other@company.com", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"requestType\":\"vacations\"", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelPtoRequestAsync_AllowsEmployeeToCancelOwnPendingDayOff()
    {
        var ownEmail = "employee@company.com";
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.SetupSequence(x => x.GetPtoRequestAsync(requestId))
            .ReturnsAsync(new PtoRequest
            {
                Id = requestId,
                UserId = userId,
                UserEmail = ownEmail,
                UserDisplayName = "Employee",
                RequestType = "day_off",
                NumberOfDays = 1,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(1),
                Status = "pending",
                RequestedByEmail = ownEmail,
                RequestedByName = "Employee",
                RequestedByRole = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            })
            .ReturnsAsync(new PtoRequest
            {
                Id = requestId,
                UserId = userId,
                UserEmail = ownEmail,
                UserDisplayName = "Employee",
                RequestType = "day_off",
                NumberOfDays = 1,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(1),
                Status = "canceled",
                RequestedByEmail = ownEmail,
                RequestedByName = "Employee",
                RequestedByRole = 0,
                ReviewedByEmail = ownEmail,
                ReviewedByName = ownEmail,
                ReviewedByRole = 0,
                ReviewedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        users.Setup(x => x.UpdatePtoRequestStatusAsync(
                requestId,
                "canceled",
                It.IsAny<string>(),
                It.IsAny<string>(),
                0,
                null))
            .ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: ownEmail);

        var result = await service.CancelPtoRequestAsync(context, requestId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("\"status\":\"canceled\"", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertCalendarPtoAsync_ReturnsForbidden_ForAdminOutsideTargetCompanyScope()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@other.com")
            .AsEmployee()
            .WithCompany("OtherCo")
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");
        var request = new UpsertPtoRequest(targetId, DateTime.UtcNow.Date.AddDays(5).ToString("yyyy-MM-dd"), 1, "vacations", "Test reason", null);

        var result = await service.UpsertCalendarPtoAsync(context, request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        users.Verify(x => x.UpsertPtoRequestAsync(It.IsAny<PtoRequest>()), Times.Never);
        users.Verify(x => x.ApplyPtoOverrideDatesAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<DateTime>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetPtoRequestAsync_ReturnsForbidden_ForManagerOutsideTargetCompanyScope()
    {
        var requestId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetPtoRequestAsync(requestId)).ReturnsAsync(new PtoRequestBuilder()
            .WithId(requestId)
            .WithUserId(targetId)
            .WithUserEmail("employee@other.com")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@other.com")
            .AsEmployee()
            .WithCompany("OtherCo")
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");

        var result = await service.GetPtoRequestAsync(context, requestId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApprovePtoRequestAsync_ReturnsForbidden_ForLegacyOverrideOutsideCompanyScope()
    {
        var groupId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetScheduleOverridesByGroupAsync(groupId)).ReturnsAsync(new[]
        {
            new UserScheduleOverride
            {
                UserId = targetId,
                GroupId = groupId,
                OverrideDate = DateTime.UtcNow.Date.AddDays(5),
                EntryType = "leave",
                RequestType = "vacations"
            }
        });
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee@other.com")
            .AsEmployee()
            .WithCompany("OtherCo")
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");

        var result = await service.ApprovePtoRequestAsync(context, groupId, new ReviewRequest("Approved for testing"));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        users.Verify(x => x.RemoveScheduleOverridesByGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetScheduleEventsAsync_ReturnsForbidden_ForEmployee()
    {
        var service = CreateService();
        var context = BuildHttpContext(role: 0, email: "employee@company.com");

        var result = await service.GetScheduleEventsAsync(context, 20);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSwapRequestsAsync_FiltersManagerResultsByCompanyScope()
    {
        var inRequesterId = Guid.NewGuid();
        var inTargetId = Guid.NewGuid();
        var outRequesterId = Guid.NewGuid();
        var outTargetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder().WithId(inRequesterId).WithEmail("employee.one@company.com").AsEmployee().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(inTargetId).WithEmail("employee.two@company.com").AsEmployee().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(outRequesterId).WithEmail("employee.one@other.com").AsEmployee().WithCompany("OtherCo").Build(),
            new UserBuilder().WithId(outTargetId).WithEmail("employee.two@other.com").AsEmployee().WithCompany("OtherCo").Build()
        });
        users.Setup(x => x.GetInactiveAsync()).ReturnsAsync(Array.Empty<User>());
        users.Setup(x => x.GetSwapRequestsAsync("pending", It.IsAny<int>())).ReturnsAsync(new[]
        {
            new SwapRequestBuilder()
                .RequestedBy(inRequesterId, "employee.one@company.com", "Employee One", 0)
                .Target(inTargetId, "employee.two@company.com", "Employee Two", 0)
                .Build(),
            new SwapRequestBuilder()
                .WithId(Guid.NewGuid())
                .RequestedBy(outRequesterId, "employee.one@other.com", "Other One", 0)
                .Target(outTargetId, "employee.two@other.com", "Other Two", 0)
                .Build()
        });

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.QueryString = new QueryString("?status=pending&take=300");

        var result = await service.GetSwapRequestsAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("employee.one@company.com", response.Body);
        Assert.DoesNotContain("employee.one@other.com", response.Body);
    }

    [Fact]
    public async Task GetSwapRequestsAsync_ReturnsOk_WhenStoredSwapJsonIsMalformed()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder().WithId(requesterId).WithEmail("employee.one@company.com").AsEmployee().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(targetId).WithEmail("employee.two@company.com").AsEmployee().WithCompany("Solvo Global").Build()
        });
        users.Setup(x => x.GetInactiveAsync()).ReturnsAsync(Array.Empty<User>());
        users.Setup(x => x.GetSwapRequestsAsync("pending", It.IsAny<int>())).ReturnsAsync(new[]
        {
            new SwapRequestBuilder()
                .RequestedBy(requesterId, "employee.one@company.com", "Employee One", 0)
                .Target(targetId, "employee.two@company.com", "Employee Two", 0)
                .WithRequestedDatesJson("not-json")
                .WithTargetDatesJson("not-json")
                .WithPairingsJson("not-json")
                .Build()
        });

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.QueryString = new QueryString("?status=pending&take=300");

        var result = await service.GetSwapRequestsAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("employee.one@company.com", response.Body);
        Assert.Contains("\"pairs\":[]", response.Body);
    }

    [Fact]
    public async Task CancelSwapRequestAsync_ReturnsForbidden_ForAdminOutsideCompanyScope()
    {
        var requestId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("admin@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("admin@company.com")
            .AsAdmin()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetSwapRequestAsync(requestId)).ReturnsAsync(new SwapRequestBuilder()
            .WithId(requestId)
            .RequestedBy(requesterId, "employee.one@other.com", "Other One", 0)
            .Target(targetId, "employee.two@other.com", "Other Two", 0)
            .Build());
        users.Setup(x => x.GetByIdAsync(requesterId)).ReturnsAsync(new UserBuilder()
            .WithId(requesterId)
            .WithEmail("employee.one@other.com")
            .AsEmployee()
            .WithCompany("OtherCo")
            .Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId)
            .WithEmail("employee.two@other.com")
            .AsEmployee()
            .WithCompany("OtherCo")
            .Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 2, email: "admin@company.com");

        var result = await service.CancelSwapRequestAsync(context, requestId);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        users.Verify(x => x.UpdateSwapRequestStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task ApproveSwapRequestAsync_SendsSummaryEmailOnlyToReviewersInSwapCompanyScope()
    {
        var requestId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var requester = new UserBuilder()
            .WithId(requesterId)
            .WithEmail("requester@solvo.com")
            .WithDisplayName("Requester")
            .AsEmployee()
            .WithCompany("Solvo Global")
            .Build();
        var target = new UserBuilder()
            .WithId(targetId)
            .WithEmail("target@solvo.com")
            .WithDisplayName("Target")
            .AsEmployee()
            .WithCompany("Solvo Global")
            .Build();
        var users = new Mock<IUserRepository>();
        var manager = new UserBuilder().WithId(Guid.NewGuid()).WithEmail("manager@solvo.com").AsManager().WithCompany("Solvo Global").Build();
        users.Setup(x => x.GetByEmailAsync(manager.Email)).ReturnsAsync(manager);
        users.Setup(x => x.GetSwapRequestAsync(requestId)).ReturnsAsync(new SwapRequestBuilder()
            .WithId(requestId)
            .RequestedBy(requesterId, requester.Email, requester.DisplayName!, requester.Role)
            .Target(targetId, target.Email, target.DisplayName!, target.Role)
            .WithPairingsJson(BuildSwapPairsJson())
            .Pending()
            .Build());
        users.Setup(x => x.GetByIdAsync(requesterId)).ReturnsAsync(requester);
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(target);
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            manager,
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("admin@solvo.com").AsAdmin().WithCompany("Solvo Global").Build(),
            new UserBuilder().WithId(Guid.NewGuid()).WithEmail("admin@other.com").AsAdmin().WithCompany("OtherCo").Build()
        });
        users.Setup(x => x.UpdateSwapRequestStatusAsync(requestId, "approved", It.IsAny<string>(), It.IsAny<string>(), 0, It.IsAny<Guid?>()))
            .ReturnsAsync(1);

        var email = new FakeEmailService();
        var service = CreateService(users: users, email: email);
        var context = BuildHttpContext(role: 1, email: manager.Email);

        var result = await service.ApproveSwapRequestAsync(context, requestId, new ReviewRequest("Approved for testing"));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        var summary = Assert.Single(email.Sent, item => item.Kind == "swap-approved-summary");
        Assert.Contains("manager@solvo.com", summary.Recipients);
        Assert.Contains("admin@solvo.com", summary.Recipients);
        Assert.DoesNotContain("admin@other.com", summary.Recipients);
    }

    [Fact]
    public async Task UpsertDailyScheduleAsync_StoresSingleDayOverride_ForManagerPeer()
    {
        var targetId = Guid.NewGuid();
        var manager = new UserBuilder().WithEmail("manager@company.com").AsManager().WithCompany("ShiftTrack").Build();
        var target = new UserBuilder().WithId(targetId).WithEmail("peer@company.com").AsManager().WithCompany("ShiftTrack").Build();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync(manager.Email)).ReturnsAsync(manager);
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(target);
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { target });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());
        UserScheduleOverride? saved = null;
        users.Setup(x => x.UpsertScheduleOverrideAsync(It.IsAny<UserScheduleOverride>()))
            .Callback<UserScheduleOverride>(item => saved = item)
            .ReturnsAsync(1);

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: manager.Email);
        var result = await service.UpsertDailyScheduleAsync(
            context,
            new UpsertDailyScheduleRequest(targetId, "2026-06-24", "10:00", "19:00", "Client coverage"));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(saved);
        Assert.Equal(targetId, saved.UserId);
        Assert.Equal(new DateTime(2026, 6, 24), saved.OverrideDate);
        Assert.Equal("daily_schedule", saved.EntryType);
        Assert.Equal("10:00", saved.StartTime);
        Assert.Equal("19:00", saved.EndTime);
        Assert.Equal("Client coverage", saved.Comments);
    }

    [Fact]
    public async Task UpsertDailyScheduleAsync_ReturnsForbidden_WhenManagerTargetsAdmin()
    {
        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com").AsManager().WithCompany("ShiftTrack").Build());
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId).WithEmail("admin@company.com").AsAdmin().WithCompany("ShiftTrack").Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        var result = await service.UpsertDailyScheduleAsync(
            context,
            new UpsertDailyScheduleRequest(targetId, "2026-06-24", "10:00", "19:00", "Coverage"));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        users.Verify(x => x.UpsertScheduleOverrideAsync(It.IsAny<UserScheduleOverride>()), Times.Never);
    }

    [Fact]
    public async Task UpsertDailyScheduleAsync_RequiresComment()
    {
        var targetId = Guid.NewGuid();
        var manager = new UserBuilder().WithEmail("manager@company.com").AsManager().WithCompany("ShiftTrack").Build();
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync(manager.Email)).ReturnsAsync(manager);
        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(new UserBuilder()
            .WithId(targetId).WithEmail("employee@company.com").AsEmployee().WithCompany("ShiftTrack").Build());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: manager.Email);
        var result = await service.UpsertDailyScheduleAsync(
            context,
            new UpsertDailyScheduleRequest(targetId, "2026-06-24", "10:00", "19:00", " "));

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Comments are required", response.Body);
    }

    [Fact]
    public async Task GetCalendarAsync_ReturnsWeeklyPayload()
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
                .WithLocation("Bogota")
                .WithCompany("ShiftTrack")
                .WithOperation("Ops")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"start\":\"08:00\",\"end\":\"17:00\",\"days\":[\"Mon\",\"Tue\"]}]")
                .Build()
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(Array.Empty<UserScheduleOverride>());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.QueryString = new QueryString("?weekStart=2026-03-09");

        var result = await service.GetCalendarAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("2026-03-09", response.Body);
        Assert.Contains("employee@company.com", response.Body);
    }

    [Fact]
    public async Task GetCalendarAsync_UsesOperationCoverageRules_WhenOperationFilterIsPresent()
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
                .WithEmail("ops-one@company.com")
                .AsEmployee()
                .WithCompany("ShiftTrack")
                .WithOperation("Ops")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\"]}]")
                .Build(),
            new UserBuilder()
                .WithEmail("other-one@company.com")
                .AsEmployee()
                .WithCompany("ShiftTrack")
                .WithOperation("Other")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Tue\"]}]")
                .Build()
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());

        var coverageRules = new Mock<ICoverageRuleRepository>();
        coverageRules.Setup(x => x.ResolveRulesAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((string company, string? operation) =>
            {
                return
                string.Equals(company, "ShiftTrack", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(operation, "Ops", StringComparison.OrdinalIgnoreCase)
                    ? CoverageRuleDefaults.OrderedDays.Select(day =>
                        CoverageRuleDefaults.Build("ShiftTrack", "Ops", day, 50, 50, 40)).ToArray()
                    : CoverageRuleDefaults.BuildDefaults(company, operation);
            });

        var service = CreateService(users: users, coverageRules: coverageRules);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.QueryString = new QueryString("?weekStart=2026-03-09&operation=Ops");

        var result = await service.GetCalendarAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("\"expectedCoverage\":50", response.Body);
        Assert.Contains("\"coverage\":100", response.Body);
        Assert.Contains("\"statusColor\":\"green\"", response.Body);
        Assert.DoesNotContain("other-one@company.com", response.Body);
        coverageRules.Verify(x => x.ResolveRulesAsync("ShiftTrack", "Ops"), Times.Once);
    }

    [Fact]
    public async Task GetCalendarAsync_UsesCompanyCoverageScope_WhenRuleRequestsCompanyGlobal()
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
                .WithEmail("ops-one@company.com")
                .AsEmployee()
                .WithCompany("ShiftTrack")
                .WithOperation("Ops")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\"]}]")
                .Build(),
            new UserBuilder()
                .WithId(Guid.NewGuid())
                .WithEmail("other-one@company.com")
                .AsEmployee()
                .WithCompany("ShiftTrack")
                .WithOperation("Other")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Tue\"]}]")
                .Build()
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());

        var coverageRules = new Mock<ICoverageRuleRepository>();
        coverageRules.Setup(x => x.ResolveRulesAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((string company, string? operation) =>
                CoverageRuleDefaults.OrderedDays.Select(day =>
                    new CoverageRule
                    {
                        CompanyName = company,
                        OperationName = operation,
                        DayOfWeek = day,
                        ExpectedCoverage = 50,
                        GreenThreshold = 50,
                        YellowThreshold = 40,
                        CalculationScope = "company",
                        IsActive = true
                    }).ToArray());

        var service = CreateService(users: users, coverageRules: coverageRules);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.QueryString = new QueryString("?weekStart=2026-03-09&operation=Ops");

        var result = await service.GetCalendarAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("\"coverage\":100", response.Body);
        Assert.Contains("\"totalAgents\":1", response.Body);
        Assert.DoesNotContain("other-one@company.com", response.Body);
    }

    [Fact]
    public async Task GetCalendarAsync_ReturnsWeeklyPayload_ForEmployee()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("employee@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("employee@company.com")
            .AsEmployee()
            .WithCompany("ShiftTrack")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee One")
                .AsEmployee()
                .WithLocation("Bogota")
                .WithCompany("ShiftTrack")
                .WithOperation("Ops")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"start\":\"08:00\",\"end\":\"17:00\",\"days\":[\"Mon\",\"Tue\"]}]")
                .Build(),
            new UserBuilder()
                .WithId(Guid.NewGuid())
                .WithEmail("manager@company.com")
                .WithDisplayName("Manager One")
                .AsManager()
                .WithLocation("Bogota")
                .WithCompany("ShiftTrack")
                .WithOperation("Ops")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"start\":\"08:00\",\"end\":\"17:00\",\"days\":[\"Mon\",\"Tue\"]}]")
                .Build(),
            new UserBuilder()
                .WithId(Guid.NewGuid())
                .WithEmail("external@company.com")
                .WithDisplayName("External One")
                .AsAdmin()
                .WithLocation("Bogota")
                .WithCompany("OtherCo")
                .WithOperation("Ops")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"start\":\"08:00\",\"end\":\"17:00\",\"days\":[\"Mon\",\"Tue\"]}]")
                .Build()
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 0, email: "employee@company.com");
        context.Request.QueryString = new QueryString("?weekStart=2026-03-09");

        var result = await service.GetCalendarAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("employee@company.com", response.Body);
        Assert.Contains("manager@company.com", response.Body);
        Assert.DoesNotContain("external@company.com", response.Body);
    }

    [Fact]
    public async Task ExportCalendarAsync_ReturnsForbidden_ForEmployee()
    {
        var service = CreateService();
        var context = BuildHttpContext(role: 0, email: "employee@company.com");

        var result = await service.ExportCalendarAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteAsync(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExportCalendarAsync_ReturnsExcel_ForManager()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithEmail("manager@company.com")
                .WithDisplayName("Manager Example")
                .AsManager()
                .WithLocation("COL")
                .WithCompany("Solvo Global")
                .WithOperation("Leaders")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"start\":\"08:00\",\"end\":\"17:00\",\"days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
                .Build()
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.QueryString = new QueryString("?weekStart=2026-03-09&operation=Leaders");

        var result = await service.ExportCalendarAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteWithHeadersAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.ContentType);
        Assert.True(response.Bytes.Length > 0);
        Assert.Contains("attachment;", response.Headers.ContentDisposition.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportCalendarAsync_FiltersRowsByCallerCompanyScope()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByEmailAsync("manager@company.com")).ReturnsAsync(new UserBuilder()
            .WithEmail("manager@company.com")
            .AsManager()
            .WithCompany("Solvo Global")
            .Build());
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            new UserBuilder()
                .WithEmail("employee@company.com")
                .WithDisplayName("Employee Example")
                .AsEmployee()
                .WithCompany("Solvo Global")
                .WithOperation("Leaders")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"start\":\"08:00\",\"end\":\"17:00\",\"days\":[\"Mon\"]}]")
                .Build(),
            new UserBuilder()
                .WithId(Guid.NewGuid())
                .WithEmail("external@otherco.com")
                .WithDisplayName("External Example")
                .AsEmployee()
                .WithCompany("OtherCo")
                .WithOperation("Leaders")
                .WithShiftTime("Morning")
                .WithScheduleBlocks("[{\"start\":\"08:00\",\"end\":\"17:00\",\"days\":[\"Mon\"]}]")
                .Build()
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());

        var service = CreateService(users: users);
        var context = BuildHttpContext(role: 1, email: "manager@company.com");
        context.Request.QueryString = new QueryString("?weekStart=2026-03-09&operation=Leaders");

        var result = await service.ExportCalendarAsync(context, context.Request);

        var response = await ResultTestHelpers.ExecuteWithHeadersAsync(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.True(response.Bytes.Length > 0);
        users.Verify(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public void ResolveWindow_UsesThreeWeeks_WhenSelectedWeekIsCurrentAndNoExtraFilters()
    {
        var currentWeekStart = CalendarHelpers.ResolveWeekStart(DateTime.UtcNow.Date);
        var filter = new CalendarExportFilter
        {
            RequestedWeekStart = currentWeekStart
        };

        var window = CalendarExportHelpers.ResolveWindow(filter);

        Assert.Equal(currentWeekStart.AddDays(-14), window.StartDate);
        Assert.Equal(currentWeekStart.AddDays(6), window.EndDate);
    }

    [Fact]
    public void ResolveWindow_UsesSelectedWeekOnly_WhenSelectedWeekIsNotCurrent()
    {
        var currentWeekStart = CalendarHelpers.ResolveWeekStart(DateTime.UtcNow.Date);
        var otherWeekStart = currentWeekStart.AddDays(-7);
        var filter = new CalendarExportFilter
        {
            RequestedWeekStart = otherWeekStart
        };

        var window = CalendarExportHelpers.ResolveWindow(filter);

        Assert.Equal(otherWeekStart, window.StartDate);
        Assert.Equal(otherWeekStart.AddDays(6), window.EndDate);
    }

    private static ScheduleWorkflowService CreateService(
        Mock<IUserRepository>? users = null,
        Mock<ICoverageRuleRepository>? coverageRules = null,
        Mock<IHolidayRepository>? holidays = null,
        FakeEmailService? email = null,
        Mock<IHubContext<ScheduleHub>>? hub = null,
        StubAuthorizationService? authorization = null)
    {
        users ??= new Mock<IUserRepository>();
        users.SetReturnsDefault(Task.FromResult<PtoRequest?>(null));
        if (coverageRules is null)
        {
            coverageRules = new Mock<ICoverageRuleRepository>();
            coverageRules.Setup(x => x.ResolveRulesAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync((string company, string? operation) => CoverageRuleDefaults.BuildDefaults(company, operation));
        }
        holidays ??= new Mock<IHolidayRepository>();
        holidays.Setup(x => x.GetActiveInRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<Holiday>());
        email ??= new FakeEmailService();
        hub ??= CreateHub();
        authorization ??= new StubAuthorizationService();

        return new ScheduleWorkflowService(
            users.Object,
            coverageRules.Object,
            holidays.Object,
            email,
            hub.Object,
            authorization,
            new StartupOptions { FrontendBaseUrl = "https://frontend.local" },
            CreateEnvironment());
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

    private static string BuildSwapPairsJson() =>
        """
        [
          {
            "RequesterCurrent": {
              "OwnerName": "Requester",
              "OwnerEmail": "requester@solvo.com",
              "Date": "2026-12-14",
              "Label": "08:00 - 17:00",
              "ShiftTime": "Morning",
              "DurationHours": 9,
              "Type": "shiftMorning"
            },
            "TargetCurrent": {
              "OwnerName": "Target",
              "OwnerEmail": "target@solvo.com",
              "Date": "2026-12-14",
              "Label": "Day Off",
              "ShiftTime": "Morning",
              "DurationHours": 0,
              "Type": "dayOff"
            },
            "RequesterResult": {
              "OwnerName": "Requester",
              "OwnerEmail": "requester@solvo.com",
              "Date": "2026-12-14",
              "Label": "Day Off",
              "ShiftTime": "Morning",
              "DurationHours": 0,
              "Type": "dayOff"
            },
            "TargetResult": {
              "OwnerName": "Target",
              "OwnerEmail": "target@solvo.com",
              "Date": "2026-12-14",
              "Label": "08:00 - 17:00",
              "ShiftTime": "Morning",
              "DurationHours": 9,
              "Type": "shiftMorning"
            }
          }
        ]
        """;

    private static Mock<IUserRepository> BuildCoveragePreviewUsers(Guid targetId, User target, string companyName = "Solvo Global", string otherOperationShift = "Morning")
    {
        var users = new Mock<IUserRepository>();
        var sameOperationPeer = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithEmail("leader.peer@solvo.com")
            .WithDisplayName("Leader Peer")
            .AsEmployee()
            .WithCompany(companyName)
            .WithOperation("Leaders")
            .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
            .Build();
        var otherOperationPeer = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithEmail("outbound.peer@solvo.com")
            .WithDisplayName("Outbound Peer")
            .AsEmployee()
            .WithCompany(companyName)
            .WithOperation("Outbound")
            .WithShiftTime(otherOperationShift)
            .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
            .Build();
        var secondOtherOperationPeer = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithEmail("outbound.two@solvo.com")
            .WithDisplayName("Outbound Two")
            .AsEmployee()
            .WithCompany(companyName)
            .WithOperation("Outbound")
            .WithShiftTime(otherOperationShift)
            .WithScheduleBlocks("[{\"Start\":\"08:00\",\"End\":\"17:00\",\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]}]")
            .Build();

        users.Setup(x => x.GetByIdAsync(targetId)).ReturnsAsync(target);
        users.Setup(x => x.GetByEmailAsync(target.Email)).ReturnsAsync(target);
        users.Setup(x => x.GetAllAsync()).ReturnsAsync(new[]
        {
            target,
            sameOperationPeer,
            otherOperationPeer,
            secondOtherOperationPeer
        });
        users.Setup(x => x.GetScheduleOverridesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<UserScheduleOverride>());

        return users;
    }

    private static Mock<ICoverageRuleRepository> BuildCoverageRules(int expectedCoverage, string calculationScope = "operation")
    {
        var coverageRules = new Mock<ICoverageRuleRepository>();
        coverageRules.Setup(x => x.ResolveRulesAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((string company, string? operation) => CoverageRuleDefaults.OrderedDays
                .Select(day =>
                {
                    var rule = CoverageRuleDefaults.Build(company, operation, day, expectedCoverage, expectedCoverage, expectedCoverage);
                    return new CoverageRule
                    {
                        CompanyName = rule.CompanyName,
                        OperationName = rule.OperationName,
                        DayOfWeek = rule.DayOfWeek,
                        ExpectedCoverage = rule.ExpectedCoverage,
                        GreenThreshold = rule.GreenThreshold,
                        YellowThreshold = rule.YellowThreshold,
                        CalculationScope = calculationScope,
                        IsActive = rule.IsActive,
                        UpdatedBy = rule.UpdatedBy,
                        UpdatedAtUtc = rule.UpdatedAtUtc
                    };
                })
                .ToArray());
        return coverageRules;
    }

    private static DateTime NextWeekday(DateTime fromDate, DayOfWeek targetDay)
    {
        var date = fromDate.Date.AddDays(1);
        while (date.DayOfWeek != targetDay)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static IWebHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IWebHostEnvironment>();
        var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "ShiftTrack.Api"));
        environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
        return environment.Object;
    }
}
