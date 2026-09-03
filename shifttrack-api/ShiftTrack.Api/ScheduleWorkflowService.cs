using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal interface IScheduleWorkflowService
{
    Task<IResult> UpsertCalendarPtoAsync(HttpContext httpContext, UpsertPtoRequest request);
    Task<IResult> PreviewCalendarPtoCoverageAsync(HttpContext httpContext, UpsertPtoRequest request);
    Task<IResult> UpsertDailyScheduleAsync(HttpContext httpContext, UpsertDailyScheduleRequest request);
    Task<IResult> GetPtoRequestAsync(HttpContext httpContext, Guid requestId);
    Task<IResult> GetPtoRequestsAsync(HttpContext httpContext, HttpRequest request);
    Task<IResult> GetPtoCoveragePreviewAsync(HttpContext httpContext, Guid requestId);
    Task<IResult> ApprovePtoRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review);
    Task<IResult> DenyPtoRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review);
    Task<IResult> CancelPtoRequestAsync(HttpContext httpContext, Guid requestId);
    Task<IResult> CreateSwapRequestAsync(HttpContext httpContext, CreateSwapRequest request);
    Task<IResult> GetSwapRequestAsync(HttpContext httpContext, Guid requestId);
    Task<IResult> GetSwapRequestsAsync(HttpContext httpContext, HttpRequest request);
    Task<IResult> ApproveSwapRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review);
    Task<IResult> DenySwapRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review);
    Task<IResult> CancelSwapRequestAsync(HttpContext httpContext, Guid requestId);
    Task<IResult> GetSwapCandidatesAsync(HttpContext httpContext, HttpRequest request);
    Task<IResult> GetCalendarAsync(HttpContext httpContext, HttpRequest request);
    Task<IResult> ExportCalendarAsync(HttpContext httpContext, HttpRequest request);
    Task<IResult> GetScheduleEventsAsync(HttpContext httpContext, int? take);
}

internal sealed partial class ScheduleWorkflowService : IScheduleWorkflowService
{
    private readonly IUserRepository _users;
    private readonly ICoverageRuleRepository _coverageRules;
    private readonly IHolidayRepository _holidays;
    private readonly IEmailService _emailService;
    private readonly IHubContext<ScheduleHub> _hub;
    private readonly IAuthorizationService _authorizationService;
    private readonly StartupOptions _options;
    private readonly IWebHostEnvironment _environment;

    public ScheduleWorkflowService(
        IUserRepository users,
        ICoverageRuleRepository coverageRules,
        IHolidayRepository holidays,
        IEmailService emailService,
        IHubContext<ScheduleHub> hub,
        IAuthorizationService authorizationService,
        StartupOptions options,
        IWebHostEnvironment environment)
    {
        _users = users;
        _coverageRules = coverageRules;
        _holidays = holidays;
        _emailService = emailService;
        _hub = hub;
        _authorizationService = authorizationService;
        _options = options;
        _environment = environment;
    }

    public async Task<IResult> UpsertCalendarPtoAsync(HttpContext httpContext, UpsertPtoRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var callerRole = callerContext.Role;

        var targetUser = await _users.GetByIdAsync(request.UserId);
        if (targetUser is null || !targetUser.IsActive) return Results.NotFound(new ErrorResponse("Employee not found."));

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !IsInCallerCompanyScope(callerUser, targetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerRole);
        if (RoleHelpers.IsEmployeeLike(callerRole) &&
            (string.IsNullOrWhiteSpace(actor.UpdatedByEmail) ||
             !string.Equals(actor.UpdatedByEmail.Trim(), targetUser.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!DateTime.TryParseExact(request.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
        {
            return Results.BadRequest(new ErrorResponse("Start Date is invalid."));
        }

        var todayUtc = DateTime.UtcNow.Date;
        if (RoleHelpers.IsEmployeeLike(callerRole))
        {
            if (parsedStart.Date < todayUtc)
            {
                return Results.BadRequest(new ErrorResponse("Employees cannot request PTO for past dates."));
            }

            if (parsedStart.Date > todayUtc.AddDays(60))
            {
                return Results.BadRequest(new ErrorResponse("Employees can only request PTO up to 60 days from today."));
            }
        }

        if (request.NumberOfDays < 1 || request.NumberOfDays > 90)
        {
            return Results.BadRequest(new ErrorResponse("Number of days must be between 1 and 90."));
        }

        var normalizedType = NormalizePtoRequestType(request.RequestType);
        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return Results.BadRequest(new ErrorResponse("Request Type is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Comments))
        {
            return Results.BadRequest(new ErrorResponse("Comments are required."));
        }

        var effectivePtoDates = await ResolveEffectivePtoDatesAsync(targetUser, normalizedType, parsedStart.Date, request.NumberOfDays);
        var effectiveEndDate = effectivePtoDates[^1];

        var duplicatedRequestError = await BuildDuplicatePtoRequestErrorAsync(
            request.UserId,
            normalizedType,
            parsedStart.Date,
            effectiveEndDate,
            request.ExistingGroupId);
        if (!string.IsNullOrWhiteSpace(duplicatedRequestError))
        {
            return Results.BadRequest(new ErrorResponse(duplicatedRequestError));
        }

        if (normalizedType == "day_off")
        {
            var dayOffValidationError = await ValidateDayOffAvailabilityAsync(targetUser, effectivePtoDates, request.ExistingGroupId);
            if (!string.IsNullOrWhiteSpace(dayOffValidationError))
            {
                return Results.BadRequest(new ErrorResponse(dayOffValidationError));
            }
        }

        var requestId = request.ExistingGroupId ?? Guid.NewGuid();
        var requestStatus = RoleHelpers.IsAdmin(callerRole) ? "approved" : "pending";
        Guid? overrideGroupId = null;
        if (requestStatus == "approved")
        {
            overrideGroupId = await _users.ApplyPtoOverrideDatesAsync(
                request.UserId,
                effectivePtoDates,
                normalizedType,
                request.Comments,
                request.ExistingGroupId ?? requestId);
        }

        var nowUtc = DateTime.UtcNow;
        var targetDisplayName = string.IsNullOrWhiteSpace(targetUser.DisplayName) ? targetUser.Email : targetUser.DisplayName;
        await _users.UpsertPtoRequestAsync(new PtoRequest
        {
            Id = requestId,
            UserId = targetUser.Id,
            UserEmail = targetUser.Email,
            UserDisplayName = targetDisplayName,
            RequestType = normalizedType,
            NumberOfDays = request.NumberOfDays,
            StartDate = parsedStart.Date,
            EndDate = effectiveEndDate,
            Comments = string.IsNullOrWhiteSpace(request.Comments) ? null : request.Comments.Trim(),
            OverrideGroupId = overrideGroupId,
            Status = requestStatus,
            RequestedByEmail = actor.UpdatedByEmail,
            RequestedByName = actor.UpdatedByName,
            RequestedByRole = actor.UpdatedByRole,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        });

        if (requestStatus == "pending")
        {
            var reviewers = (await _users.GetAllAsync())
                .Where(u =>
                    u.IsActive &&
                    IsInCallerCompanyScope(u, targetUser) &&
                    RoleHelpers.CanReviewPto(u.Role))
                .Select(u => u.Email)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var reviewLink = ApiHelpers.BuildPtoReviewLink(_options.FrontendBaseUrl, requestId);
            await _emailService.SendPtoApprovalEmailAsync(
                reviewers,
                targetDisplayName,
                targetUser.Email,
                normalizedType,
                request.NumberOfDays,
                parsedStart.Date.ToString("yyyy-MM-dd"),
                effectiveEndDate.ToString("yyyy-MM-dd"),
                request.Comments,
                reviewLink);
        }
        else
        {
            await _emailService.SendPtoApprovedEmailAsync(
                targetUser.Email,
                targetDisplayName,
                normalizedType,
                request.NumberOfDays,
                parsedStart.Date.ToString("yyyy-MM-dd"),
                effectiveEndDate.ToString("yyyy-MM-dd"),
                string.IsNullOrWhiteSpace(actor.UpdatedByName) ? actor.UpdatedByEmail : actor.UpdatedByName,
                request.Comments);
        }

        var start = parsedStart.Date;
        var end = effectiveEndDate;
        foreach (var impactedWeek in effectivePtoDates.Select(ResolveWeekStart).Distinct())
        {
            if (requestStatus == "approved")
            {
                await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
            }
        }

        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            requestStatus == "pending" ? "pto_requested" : "pto_updated",
            targetUser.Id,
            targetUser.Email,
            actor,
            JsonSerializer.Serialize(new
            {
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = end.ToString("yyyy-MM-dd"),
                requestType = normalizedType,
                numberOfDays = request.NumberOfDays,
                groupId = requestId,
                comments = request.Comments
            }));

        return Results.Ok(new
        {
            message = requestStatus == "approved" ? "PTO updated." : "PTO request submitted for approval.",
            groupId = requestId,
            status = requestStatus,
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd")
        });
    }

    public async Task<IResult> PreviewCalendarPtoCoverageAsync(HttpContext httpContext, UpsertPtoRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var targetUser = await _users.GetByIdAsync(request.UserId);
        if (targetUser is null || !targetUser.IsActive)
        {
            return Results.NotFound(new ErrorResponse("Employee not found."));
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !IsInCallerCompanyScope(callerUser, targetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        if (RoleHelpers.IsEmployeeLike(callerContext.Role) &&
            (string.IsNullOrWhiteSpace(actor.UpdatedByEmail) ||
             !string.Equals(actor.UpdatedByEmail.Trim(), targetUser.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!DateTime.TryParseExact(request.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
        {
            return Results.BadRequest(new ErrorResponse("Start Date is invalid."));
        }

        if (request.NumberOfDays < 1 || request.NumberOfDays > 90)
        {
            return Results.BadRequest(new ErrorResponse("Number of days must be between 1 and 90."));
        }

        var normalizedType = NormalizePtoRequestType(request.RequestType);
        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return Results.BadRequest(new ErrorResponse("Request Type is required."));
        }

        var effectiveDates = await ResolveEffectivePtoDatesAsync(targetUser, normalizedType, parsedStart.Date, request.NumberOfDays);

        var duplicateRequestError = await BuildDuplicatePtoRequestErrorAsync(
            request.UserId,
            normalizedType,
            parsedStart.Date,
            effectiveDates[^1],
            request.ExistingGroupId);
        if (!string.IsNullOrWhiteSpace(duplicateRequestError))
        {
            return Results.BadRequest(new ErrorResponse(duplicateRequestError));
        }

        if (normalizedType == "day_off")
        {
            var dayOffValidationError = await ValidateDayOffAvailabilityAsync(targetUser, effectiveDates, request.ExistingGroupId);
            if (!string.IsNullOrWhiteSpace(dayOffValidationError))
            {
                return Results.BadRequest(new ErrorResponse(dayOffValidationError));
            }
        }
        var warnings = await BuildCoverageImpactWarningsAsync(callerUser, request.UserId, effectiveDates, BuildCoverageFilter(request));

        return Results.Ok(new PtoCoveragePreviewResponse
        {
            HasImpact = warnings.Length > 0,
            Warnings = warnings
        });
    }

    public async Task<IResult> GetPtoRequestAsync(HttpContext httpContext, Guid requestId)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanReviewPto(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var request = await _users.GetPtoRequestAsync(requestId) ?? await _users.GetLatestPtoRequestByGroupIdAsync(requestId);
        if (request is null)
        {
            return Results.NotFound(new ErrorResponse("PTO request not found."));
        }

        var targetUser = await _users.GetByIdAsync(request.UserId);
        if (targetUser is null)
        {
            return Results.NotFound(new ErrorResponse("Employee not found."));
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !IsInCallerCompanyScope(callerUser, targetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            httpContext.User,
            new PtoReviewResource(targetUser.Role, request.RequestedByRole),
            "CanReviewPto");
        if (!authorizationResult.Succeeded)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Ok(ToPtoRequestResponse(request));
    }

    public async Task<IResult> GetPtoRequestsAsync(HttpContext httpContext, HttpRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var canReview = RoleHelpers.CanReviewPto(callerContext.Role);
        var canViewOwnRequests = RoleHelpers.IsEmployeeLike(callerContext.Role);
        if (!canReview && !canViewOwnRequests)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var status = request.Query["status"].FirstOrDefault();
        var takeQuery = request.Query["take"].FirstOrDefault();
        var take = int.TryParse(takeQuery, out var parsedTake) ? parsedTake : 100;
        var requests = (await _users.GetPtoRequestsAsync(status, take)).ToArray();

        if (canViewOwnRequests && !canReview)
        {
            var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
            if (string.IsNullOrWhiteSpace(actor.UpdatedByEmail))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            requests = requests
                .Where(item => string.Equals(item.UserEmail, actor.UpdatedByEmail, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        else if (canReview)
        {
            var callerUser = await ResolveCallerUserAsync(callerContext);
            if (callerUser is null)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var visibleUserIds = (await _users.GetAllAsync())
                .Concat(await _users.GetInactiveAsync())
                .Where(user => IsInCallerCompanyScope(callerUser, user))
                .Select(user => user.Id)
                .ToHashSet();
            requests = requests
                .Where(item => visibleUserIds.Contains(item.UserId))
                .ToArray();
        }

        return Results.Ok(requests.Select(ToPtoRequestResponse));
    }

    public async Task<IResult> GetPtoCoveragePreviewAsync(HttpContext httpContext, Guid requestId)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanReviewPto(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var request = await _users.GetPtoRequestAsync(requestId) ?? await _users.GetLatestPtoRequestByGroupIdAsync(requestId);
        if (request is null)
        {
            return Results.NotFound(new ErrorResponse("PTO request not found."));
        }

        var targetUser = await _users.GetByIdAsync(request.UserId);
        if (targetUser is null)
        {
            return Results.NotFound(new ErrorResponse("Employee not found."));
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !IsInCallerCompanyScope(callerUser, targetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            httpContext.User,
            new PtoReviewResource(targetUser.Role, request.RequestedByRole),
            "CanReviewPto");
        if (!authorizationResult.Succeeded)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var warnings = await BuildCoverageImpactWarningsAsync(callerUser, request.UserId, EnumerateInclusiveDates(request.StartDate, request.EndDate));
        return Results.Ok(new PtoCoveragePreviewResponse
        {
            HasImpact = warnings.Length > 0,
            Warnings = warnings
        });
    }

    public async Task<IResult> GetCalendarAsync(HttpContext httpContext, HttpRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var weekStartQuery = request.Query["weekStart"].FirstOrDefault();
        var startDate = ResolveWeekStartFromQuery(weekStartQuery) ?? ResolveWeekStart(DateTime.UtcNow.Date);
        var filter = BuildCoverageFilter(request);

        var days = Enumerable.Range(0, 7).Select(i => startDate.AddDays(i)).ToArray();
        var allUsers = (await _users.GetAllAsync())
            .Where(u => u.IsActive)
            .Where(u => IsInCallerCompanyScope(callerUser, u))
            .ToArray();
        var overrides = (await _users.GetScheduleOverridesAsync(startDate, startDate.AddDays(6))).ToArray();
        var overrideMap = overrides.ToDictionary(o => $"{o.UserId:N}|{o.OverrideDate:yyyy-MM-dd}", o => o, StringComparer.OrdinalIgnoreCase);

        var relevantUsers = allUsers
            .Where(u => HasCalendarPresence(u, days, overrideMap))
            .ToArray();

        var allRows = relevantUsers.Select(u => BuildCalendarRow(u, days, overrideMap)).ToArray();
        var filteredRows = ApplyCalendarRowFilters(allRows, filter);
        var (coverageRows, rules) = await ResolveCoverageRowsAndRulesAsync(allRows, filteredRows, filter.HasActiveFilters);
        var coverageCalculated = BuildCoverage(days, coverageRows, coverageRows.Length, rules);

        if (startDate < ResolveWeekStart(DateTime.UtcNow.Date))
        {
            var snapshotScope = ResolveCoverageScope(allRows);
            var snapshotRules = await _coverageRules.ResolveRulesAsync(snapshotScope.CompanyName, snapshotScope.OperationName);
            var snapshotRows = IsCompanyCalculationScope(snapshotRules) && !string.IsNullOrWhiteSpace(snapshotScope.CompanyName)
                ? allRows.Where(row => string.Equals(row.Company, snapshotScope.CompanyName, StringComparison.OrdinalIgnoreCase)).ToArray()
                : allRows;
            var snapshotCoverage = BuildCoverage(days, snapshotRows, snapshotRows.Length, snapshotRules);
            await _users.SaveCoverageSnapshotAsync(new WeeklyCoverageSnapshot
            {
                WeekStartDate = startDate,
                PayloadJson = JsonSerializer.Serialize(snapshotCoverage),
                ItemsJson = JsonSerializer.Serialize(allRows),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        return Results.Ok(new CalendarResponse
        {
            WeekStart = startDate.ToString("yyyy-MM-dd"),
            WeekEnd = startDate.AddDays(6).ToString("yyyy-MM-dd"),
            Days = days.Select(d => new DayDescriptor
            {
                Date = d.ToString("yyyy-MM-dd"),
                Label = d.ToString("ddd MMM d", CultureInfo.InvariantCulture)
            }).ToArray(),
            Coverage = coverageCalculated,
            Items = filteredRows
        });
    }

    public async Task<IResult> GetScheduleEventsAsync(HttpContext httpContext, int? take)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var normalizedTake = Math.Clamp(take ?? 20, 1, 100);
        var events = await _users.GetRecentScheduleEventsAsync(normalizedTake);
        var scopedEmails = (await _users.GetAllAsync())
            .Concat(await _users.GetInactiveAsync())
            .Where(user => IsInCallerCompanyScope(callerUser, user))
            .Select(user => user.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var payload = events.Select(e => new ScheduleEventResponse
        {
            Id = e.Id,
            EmployeeId = e.EmployeeId?.ToString() ?? string.Empty,
            EmployeeEmail = e.EmployeeEmail,
            Action = e.Action,
            UpdatedByUserId = e.UpdatedByUserId?.ToString() ?? string.Empty,
            UpdatedByEmail = e.UpdatedByEmail,
            UpdatedByName = e.UpdatedByName,
            UpdatedByRole = e.UpdatedByRole,
            OccurredAtUtc = DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc).ToString("O"),
            PayloadJson = e.PayloadJson
        }).Where(e => scopedEmails.Contains(e.EmployeeEmail) || scopedEmails.Contains(e.UpdatedByEmail));
        return Results.Ok(payload);
    }

    public Task<IResult> ApprovePtoRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review) =>
        ReviewPtoRequestAsync(httpContext, requestId, ReviewAction.Approve, review);

    public Task<IResult> DenyPtoRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review) =>
        ReviewPtoRequestAsync(httpContext, requestId, ReviewAction.Deny, review);

    public async Task<IResult> CancelPtoRequestAsync(HttpContext httpContext, Guid requestId)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var callerRole = callerContext.Role;
        var canReview = RoleHelpers.CanReviewPto(callerRole);
        var canSelfCancelDayOff = RoleHelpers.IsEmployeeLike(callerRole);
        if (!canReview && !canSelfCancelDayOff)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var request = await _users.GetPtoRequestAsync(requestId);
        if (request is null)
        {
            return Results.NotFound(new ErrorResponse("PTO request not found."));
        }

        if (!canReview)
        {
            var selfActor = ApiHelpers.ExtractActor(httpContext, callerRole);
            if (!string.Equals(request.RequestType, "day_off", StringComparison.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!string.Equals(request.UserEmail, selfActor.UpdatedByEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!string.Equals(request.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ErrorResponse("You can only cancel pending Day Off requests."));
            }

            await _users.UpdatePtoRequestStatusAsync(requestId, "canceled", selfActor.UpdatedByEmail, selfActor.UpdatedByName, selfActor.UpdatedByRole, null);

            await ApiHelpers.PublishScheduleEventAsync(
                _users,
                _hub,
                "pto_canceled",
                request.UserId,
                request.UserEmail,
                selfActor,
                JsonSerializer.Serialize(new { requestId = request.Id, requestType = request.RequestType }));

            var selfCanceled = await _users.GetPtoRequestAsync(requestId);
            return Results.Ok(ToPtoRequestResponse(selfCanceled ?? request));
        }

        if (!string.Equals(request.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ErrorResponse("Only approved PTO can be canceled."));
        }

        var targetUser = await _users.GetByIdAsync(request.UserId);
        if (targetUser is null)
        {
            return Results.NotFound(new ErrorResponse("Employee not found."));
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !IsInCallerCompanyScope(callerUser, targetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            httpContext.User,
            new PtoReviewResource(targetUser.Role, request.RequestedByRole),
            "CanReviewPto");
        if (!authorizationResult.Succeeded)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerRole);
        await _users.UpdatePtoRequestStatusAsync(requestId, "canceled", actor.UpdatedByEmail, actor.UpdatedByName, actor.UpdatedByRole, null);

        var appliedGroupId = request.OverrideGroupId ?? request.Id;
        await _users.RemoveScheduleOverridesByGroupAsync(request.UserId, appliedGroupId);

        foreach (var impactedWeek in EnumerateInclusiveDates(request.StartDate, request.EndDate).Select(ResolveWeekStart).Distinct())
        {
            await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
        }

        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            "pto_canceled",
            request.UserId,
            request.UserEmail,
            actor,
            JsonSerializer.Serialize(new { requestId = request.Id, requestType = request.RequestType }));

        var updated = await _users.GetPtoRequestAsync(requestId);
        return Results.Ok(ToPtoRequestResponse(updated ?? request));
    }

    private async Task<IResult> ReviewPtoRequestAsync(HttpContext httpContext, Guid requestId, ReviewAction action, ReviewRequest review)
    {
        var reviewComments = review.Comments?.Trim();
        if (string.IsNullOrWhiteSpace(reviewComments))
        {
            return Results.BadRequest(new ErrorResponse("A review comment is required."));
        }
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanReviewPto(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var callerRole = callerContext.Role;

        var request = await _users.GetPtoRequestAsync(requestId) ?? await _users.GetLatestPtoRequestByGroupIdAsync(requestId);
        if (request is null)
        {
            return action == ReviewAction.Deny
                ? await HandleLegacyDenyOrCancelAsync(httpContext, requestId, callerContext)
                : await HandleLegacyApproveCancellationAsync(httpContext, requestId, callerContext);
        }

        var targetUser = await _users.GetByIdAsync(request.UserId);
        if (targetUser is null)
        {
            return Results.NotFound(new ErrorResponse("Employee not found."));
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !IsInCallerCompanyScope(callerUser, targetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            httpContext.User,
            new PtoReviewResource(targetUser.Role, request.RequestedByRole),
            "CanReviewPto");
        if (!authorizationResult.Succeeded)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return action == ReviewAction.Approve
            ? await ApproveCurrentRequestAsync(httpContext, requestId, request, callerRole, reviewComments)
            : await DenyCurrentRequestAsync(httpContext, requestId, request, callerRole, reviewComments);
    }

    private async Task<IResult> ApproveCurrentRequestAsync(HttpContext httpContext, Guid requestId, PtoRequest request, int callerRole, string reviewComments)
    {
        if (string.Equals(request.Status, "denied", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ErrorResponse("PTO request is already denied."));
        }

        if (!string.Equals(request.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            var actor = ApiHelpers.ExtractActor(httpContext, callerRole);
            var appliedDates = EnumerateInclusiveDates(request.StartDate, request.EndDate).ToArray();
            var appliedGroupId = await _users.ApplyPtoOverrideDatesAsync(
                request.UserId,
                appliedDates,
                request.RequestType,
                request.Comments,
                request.OverrideGroupId ?? request.Id);

            await _users.UpdatePtoRequestStatusAsync(requestId, "approved", actor.UpdatedByEmail, actor.UpdatedByName, actor.UpdatedByRole, appliedGroupId, reviewComments);
            foreach (var impactedWeek in appliedDates.Select(ResolveWeekStart).Distinct())
            {
                await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
            }

            await ApiHelpers.PublishScheduleEventAsync(
                _users,
                _hub,
                "pto_approved",
                request.UserId,
                request.UserEmail,
                actor,
                JsonSerializer.Serialize(new { requestId = request.Id, requestType = request.RequestType }));

            await _emailService.SendPtoApprovedEmailAsync(
                request.UserEmail,
                request.UserDisplayName,
                request.RequestType,
                request.NumberOfDays,
                request.StartDate.ToString("yyyy-MM-dd"),
                request.EndDate.ToString("yyyy-MM-dd"),
                string.IsNullOrWhiteSpace(actor.UpdatedByName) ? actor.UpdatedByEmail : actor.UpdatedByName,
                reviewComments);
        }

        var updated = await _users.GetPtoRequestAsync(requestId);
        return Results.Ok(ToPtoRequestResponse(updated ?? request));
    }

    private async Task<IResult> DenyCurrentRequestAsync(HttpContext httpContext, Guid requestId, PtoRequest request, int callerRole, string reviewComments)
    {
        if (string.Equals(request.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ErrorResponse("Approved PTO cannot be denied from this endpoint."));
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerRole);
        await _users.UpdatePtoRequestStatusAsync(requestId, "denied", actor.UpdatedByEmail, actor.UpdatedByName, actor.UpdatedByRole, null, reviewComments);
        if (request.OverrideGroupId.HasValue)
        {
            await _users.RemoveScheduleOverridesByGroupAsync(request.UserId, request.OverrideGroupId.Value);
            foreach (var impactedWeek in EnumerateInclusiveDates(request.StartDate, request.EndDate).Select(ResolveWeekStart).Distinct())
            {
                await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
            }
        }

        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            "pto_denied",
            request.UserId,
            request.UserEmail,
            actor,
            JsonSerializer.Serialize(new { requestId = request.Id, requestType = request.RequestType }));

        await _emailService.SendPtoDeniedEmailAsync(
            request.UserEmail,
            request.UserDisplayName,
            request.RequestType,
            request.NumberOfDays,
            request.StartDate.ToString("yyyy-MM-dd"),
            request.EndDate.ToString("yyyy-MM-dd"),
            string.IsNullOrWhiteSpace(actor.UpdatedByName) ? actor.UpdatedByEmail : actor.UpdatedByName,
            reviewComments);

        var updated = await _users.GetPtoRequestAsync(requestId);
        return Results.Ok(ToPtoRequestResponse(updated ?? request));
    }

    private async Task<IResult> HandleLegacyApproveCancellationAsync(HttpContext httpContext, Guid requestId, CallerContext callerContext)
    {
        var overridesInGroup = (await _users.GetScheduleOverridesByGroupAsync(requestId)).ToArray();
        if (overridesInGroup.Length == 0)
        {
            return Results.NotFound(new ErrorResponse("PTO request not found."));
        }

        var legacyUserId = overridesInGroup[0].UserId;
        var legacyTargetUser = await _users.GetByIdAsync(legacyUserId);
        if (legacyTargetUser is null)
        {
            return Results.NotFound(new ErrorResponse("Employee not found."));
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null || !IsInCallerCompanyScope(callerUser, legacyTargetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var callerRole = callerContext.Role;
        if (RoleHelpers.IsManager(callerRole) && RoleHelpers.IsAdmin(legacyTargetUser.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var legacyAuthorization = await _authorizationService.AuthorizeAsync(
            httpContext.User,
            new PtoReviewResource(legacyTargetUser.Role, 0),
            "CanReviewPto");
        if (!legacyAuthorization.Succeeded)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var legacyActor = ApiHelpers.ExtractActor(httpContext, callerRole);
        await _users.RemoveScheduleOverridesByGroupAsync(legacyUserId, requestId);
        foreach (var impactedWeek in overridesInGroup.Select(o => ResolveWeekStart(o.OverrideDate)).Distinct())
        {
            await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
        }

        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            "pto_canceled",
            legacyUserId,
            legacyTargetUser.Email,
            legacyActor,
            JsonSerializer.Serialize(new { requestId, requestType = overridesInGroup[0].RequestType ?? "pto" }));

        return Results.Ok(new { message = "PTO canceled." });
    }

    private async Task<IResult> HandleLegacyDenyOrCancelAsync(HttpContext httpContext, Guid requestId, CallerContext callerContext)
    {
        var overridesInGroup = (await _users.GetScheduleOverridesByGroupAsync(requestId)).ToArray();
        if (overridesInGroup.Length > 0)
        {
            var userId = overridesInGroup[0].UserId;
            var target = await _users.GetByIdAsync(userId);
            if (target is null)
            {
                return Results.NotFound(new ErrorResponse("Employee not found."));
            }

            var callerUser = await ResolveCallerUserAsync(callerContext);
            if (callerUser is null || !IsInCallerCompanyScope(callerUser, target))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var callerRole = callerContext.Role;
            if (RoleHelpers.IsManager(callerRole) && RoleHelpers.IsAdmin(target.Role))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var legacyAuthorization = await _authorizationService.AuthorizeAsync(
                httpContext.User,
                new PtoReviewResource(target.Role, 0),
                "CanReviewPto");
            if (!legacyAuthorization.Succeeded)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            await _users.RemoveScheduleOverridesByGroupAsync(userId, requestId);
            foreach (var impactedWeek in overridesInGroup.Select(o => ResolveWeekStart(o.OverrideDate)).Distinct())
            {
                await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
            }

            return Results.Ok(new { message = "PTO canceled." });
        }

        var snapshotWeek = await _users.FindCoverageSnapshotWeekByGroupIdAsync(requestId);
        if (snapshotWeek.HasValue)
        {
            await RebuildWeekSnapshotAsync(_users, _coverageRules, snapshotWeek.Value);
            return Results.Ok(new { message = "PTO canceled." });
        }

        return Results.NotFound(new ErrorResponse("PTO request not found."));
    }

    private async Task<DateTime[]> ResolveEffectivePtoDatesAsync(User targetUser, string normalizedType, DateTime startDate, int numberOfDays)
    {
        if (!string.Equals(normalizedType, "vacations", StringComparison.OrdinalIgnoreCase) ||
            !HasColombianLocation(targetUser.Location))
        {
            return Enumerable.Range(0, numberOfDays).Select(offset => startDate.Date.AddDays(offset)).ToArray();
        }

        var holidays = (await _holidays.GetActiveInRangeAsync(startDate.Date, startDate.Date.AddDays(366), "CO"))
            .Select(item => item.Date.Date)
            .ToHashSet();

        var dates = new List<DateTime>();
        var countedBusinessDays = 0;
        var cursor = startDate.Date;

        while (countedBusinessDays < numberOfDays)
        {
            dates.Add(cursor);
            if (IsVacationBusinessDay(cursor, holidays))
            {
                countedBusinessDays++;
            }

            cursor = cursor.AddDays(1);
        }

        return dates.ToArray();
    }

    private static bool IsVacationBusinessDay(DateTime date, HashSet<DateTime> holidays) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday &&
        !holidays.Contains(date.Date);

    private static bool HasColombianLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return false;
        var normalized = NormalizeSearch(location);
        return normalized is "colombia" or "co" or "col";
    }

    private static IEnumerable<DateTime> EnumerateInclusiveDates(DateTime startDate, DateTime endDate)
    {
        for (var current = startDate.Date; current <= endDate.Date; current = current.AddDays(1))
        {
            yield return current;
        }
    }

    private async Task<CoverageImpactWarningResponse[]> BuildCoverageImpactWarningsAsync(User callerUser, Guid userId, IEnumerable<DateTime> effectiveDates, CalendarCoverageFilter? filter = null)
    {
        var orderedDates = effectiveDates
            .Select(item => item.Date)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        if (orderedDates.Length == 0)
        {
            return Array.Empty<CoverageImpactWarningResponse>();
        }

        var warnings = new List<CoverageImpactWarningResponse>();
        var allUsers = (await _users.GetAllAsync())
            .Where(user => user.IsActive)
            .Where(user => IsInCallerCompanyScope(callerUser, user))
            .ToArray();
        var targetUserForScope = allUsers.FirstOrDefault(user => user.Id == userId);
        if (targetUserForScope is null)
        {
            return Array.Empty<CoverageImpactWarningResponse>();
        }

        foreach (var weekGroup in orderedDates.GroupBy(ResolveWeekStart))
        {
            var weekStart = weekGroup.Key;
            var weekDays = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset)).ToArray();
            var overrides = (await _users.GetScheduleOverridesAsync(weekStart, weekStart.AddDays(6))).ToArray();
            var overrideMap = overrides.ToDictionary(item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}", item => item, StringComparer.OrdinalIgnoreCase);
            var relevantUsers = allUsers.Where(user => HasCalendarPresence(user, weekDays, overrideMap)).ToArray();
            var allRows = relevantUsers.Select(user => BuildCalendarRow(user, weekDays, overrideMap)).ToArray();
            var effectiveFilter = filter ?? CalendarCoverageFilter.Empty;
            var filteredRows = ApplyCalendarRowFilters(allRows, effectiveFilter);
            var (coverageRows, rules) = await ResolveCoverageRowsAndRulesAsync(allRows, filteredRows, effectiveFilter.HasActiveFilters);
            var targetRow = coverageRows.FirstOrDefault(row => row.Id == userId);
            if (targetRow is null) continue;

            var coverageByDate = BuildCoverage(weekDays, coverageRows, coverageRows.Length, rules).ToDictionary(item => item.Date);

            foreach (var impactedDate in weekGroup.OrderBy(item => item))
            {
                var dayKey = impactedDate.ToString("yyyy-MM-dd");
                var targetCell = targetRow.Cells.FirstOrDefault(cell => cell.Date == dayKey);
                if (!IsWorkingCell(targetCell)) continue;
                if (!coverageByDate.TryGetValue(dayKey, out var summary)) continue;

                var coverageAgentCount = coverageRows.Length;
                var requiredAgents = (int)Math.Ceiling(coverageAgentCount * (summary.ExpectedCoverage / 100.0));
                var projectedWorkingAgents = Math.Max(0, summary.TotalAgents - 1);
                if (projectedWorkingAgents >= requiredAgents) continue;
                var projectedCoverage = coverageAgentCount == 0
                    ? 0
                    : Math.Round((projectedWorkingAgents * 100.0) / coverageAgentCount, 1);

                warnings.Add(new CoverageImpactWarningResponse
                {
                    Date = dayKey,
                    RequiredAgents = requiredAgents,
                    CurrentWorkingAgents = summary.TotalAgents,
                    ProjectedWorkingAgents = projectedWorkingAgents,
                    Message = $"Authorizing this PTO / Day Off will impact coverage. For {impactedDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)}, active agents will total {projectedWorkingAgents} and coverage will drop to {projectedCoverage:0.#}%. You must guarantee at least {requiredAgents} agents for that day."
                });
            }
        }

        return warnings.ToArray();
    }

    private async Task<string?> ValidateDayOffAvailabilityAsync(User targetUser, IEnumerable<DateTime> effectiveDates, Guid? existingGroupId)
    {
        var orderedDates = effectiveDates
            .Select(item => item.Date)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        foreach (var weekGroup in orderedDates.GroupBy(ResolveWeekStart))
        {
            var weekStart = weekGroup.Key;
            var overrides = (await _users.GetScheduleOverridesAsync(weekStart, weekStart.AddDays(6))).ToArray();
            var overrideMap = overrides.ToDictionary(
                item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}",
                item => item,
                StringComparer.OrdinalIgnoreCase);

            foreach (var impactedDate in weekGroup)
            {
                var cell = ResolveCalendarCellForDate(targetUser, impactedDate, overrideMap);
                if (!string.Equals(cell.Type, "dayOff", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (existingGroupId.HasValue &&
                    Guid.TryParse(cell.PtoGroupId, out var existingCellGroupId) &&
                    existingCellGroupId == existingGroupId.Value)
                {
                    continue;
                }

                return $"You cannot request a Day Off on {impactedDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)} because that day is already marked as Day Off.";
            }
        }

        return null;
    }

    private enum ReviewAction
    {
        Approve,
        Deny
    }

    private async Task<string?> BuildDuplicatePtoRequestErrorAsync(
        Guid userId,
        string normalizedRequestedType,
        DateTime startDate,
        DateTime endDate,
        Guid? existingGroupId)
    {
        var duplicated = await _users.GetOverlappingActivePtoRequestAsync(userId, startDate, endDate, existingGroupId);
        if (duplicated is null)
        {
            return null;
        }

        var existingTypeLabel = string.Equals(duplicated.RequestType, "day_off", StringComparison.OrdinalIgnoreCase) ? "Day Off" : "PTO";
        var requestedTypeLabel = string.Equals(normalizedRequestedType, "day_off", StringComparison.OrdinalIgnoreCase) ? "Day Off" : "PTO";
        var effectiveTypeLabel = string.Equals(existingTypeLabel, requestedTypeLabel, StringComparison.OrdinalIgnoreCase)
            ? requestedTypeLabel
            : "PTO/Day Off";

        return $"A request has already been detected for this date.{Environment.NewLine}" +
               $"You have already created a {effectiveTypeLabel} request for this date.{Environment.NewLine}" +
               "Please review your existing request before submitting a new one.";
    }

    private async Task<User?> ResolveCallerUserAsync(CallerContext callerContext)
    {
        if (callerContext.UserId.HasValue)
        {
            var byId = await _users.GetByIdAsync(callerContext.UserId.Value);
            if (byId is not null && byId.IsActive)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(callerContext.Email)
            ? null
            : await _users.GetByEmailAsync(callerContext.Email);
    }

    private static bool IsInCallerCompanyScope(User callerUser, User targetUser) =>
        CompanyScopeHelpers.IsInCallerCompanyScope(callerUser, targetUser);

    private static bool IsCompanyCalculationScope(IEnumerable<CoverageRule> rules) =>
        rules.Any(rule => string.Equals(rule.CalculationScope, "company", StringComparison.OrdinalIgnoreCase));

    private async Task<(CalendarRow[] Rows, CoverageRule[] Rules)> ResolveCoverageRowsAndRulesAsync(CalendarRow[] allRows, CalendarRow[] filteredRows, bool hasActiveFilters)
    {
        var coverageScope = ResolveCoverageScope(filteredRows);
        if (string.IsNullOrWhiteSpace(coverageScope.CompanyName))
        {
            coverageScope = ResolveCoverageScope(allRows);
        }

        var rules = await _coverageRules.ResolveRulesAsync(coverageScope.CompanyName, coverageScope.OperationName);
        var coverageRows = !hasActiveFilters && IsCompanyCalculationScope(rules) && !string.IsNullOrWhiteSpace(coverageScope.CompanyName)
            ? allRows.Where(row => string.Equals(row.Company, coverageScope.CompanyName, StringComparison.OrdinalIgnoreCase)).ToArray()
            : filteredRows;

        return (coverageRows.Length == 0 ? filteredRows : coverageRows, rules);
    }

    private static CalendarCoverageFilter BuildCoverageFilter(HttpRequest request) => new(
        request.Query["employee"].FirstOrDefault(),
        request.Query["role"].FirstOrDefault(),
        request.Query["shift"].FirstOrDefault(),
        request.Query["operation"].FirstOrDefault(),
        request.Query["company"].FirstOrDefault());

    private static CalendarCoverageFilter BuildCoverageFilter(UpsertPtoRequest request) => new(
        request.EmployeeFilter,
        request.RoleFilter,
        request.ShiftFilter,
        request.OperationFilter,
        request.CompanyFilter);

    private static CalendarRow[] ApplyCalendarRowFilters(IEnumerable<CalendarRow> rows, CalendarCoverageFilter filter)
    {
        var employeeFilter = NormalizeSearch(filter.EmployeeFilter ?? string.Empty);
        var shiftFilter = filter.ShiftFilter?.Trim();
        var operationFilter = filter.OperationFilter?.Trim();
        var companyFilter = filter.CompanyFilter?.Trim();
        var hasRoleFilter = int.TryParse(filter.RoleFilter?.Trim(), out var roleFilter) && RoleHelpers.IsKnownRole(roleFilter);

        return rows.Where(row =>
        {
            if (!string.IsNullOrWhiteSpace(employeeFilter))
            {
                var haystack = NormalizeSearch($"{row.DisplayName} {row.Email}");
                if (!haystack.Contains(employeeFilter)) return false;
            }

            if (!string.IsNullOrWhiteSpace(shiftFilter) && !string.Equals(row.ShiftTime, shiftFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(operationFilter) && !string.Equals(row.Operation, operationFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(companyFilter) && !string.Equals(row.Company, companyFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (hasRoleFilter && row.Role != roleFilter) return false;
            return true;
        }).ToArray();
    }
}

internal sealed record CalendarCoverageFilter(
    string? EmployeeFilter,
    string? RoleFilter,
    string? ShiftFilter,
    string? OperationFilter,
    string? CompanyFilter)
{
    public static readonly CalendarCoverageFilter Empty = new(null, null, null, null, null);

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(EmployeeFilter) ||
        !string.IsNullOrWhiteSpace(RoleFilter) ||
        !string.IsNullOrWhiteSpace(ShiftFilter) ||
        !string.IsNullOrWhiteSpace(OperationFilter) ||
        !string.IsNullOrWhiteSpace(CompanyFilter);
}
