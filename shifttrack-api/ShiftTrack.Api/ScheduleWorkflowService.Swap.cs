using System.Globalization;
using System.Text.Json;
using ShiftTrack.Application;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal sealed partial class ScheduleWorkflowService
{
    public async Task<IResult> CreateSwapRequestAsync(HttpContext httpContext, CreateSwapRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        if (string.IsNullOrWhiteSpace(actor.UpdatedByEmail))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var requester = await _users.GetByEmailAsync(actor.UpdatedByEmail);
        if (requester is null || !requester.IsActive)
        {
            return Results.NotFound(new ErrorResponse("Requester not found."));
        }

        var target = await _users.GetByIdAsync(request.TargetUserId);
        if (target is null || !target.IsActive)
        {
            return Results.NotFound(new ErrorResponse("Target employee not found."));
        }

        if (!IsInCallerCompanyScope(requester, target))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (requester.Id == target.Id)
        {
            return Results.BadRequest(new ErrorResponse("You cannot create a swap request with yourself."));
        }

        if (requester.Role != target.Role || requester.Role != callerContext.Role)
        {
            return Results.BadRequest(new ErrorResponse("Swap requests can only be created with another active employee of the same role."));
        }

        var requesterDates = ParseDistinctDates(request.RequestedDates);
        var targetDates = ParseDistinctDates(request.TargetDates);
        if (requesterDates.Length == 0 || targetDates.Length == 0)
        {
            return Results.BadRequest(new ErrorResponse("An offered date and requested coworker day off date are required."));
        }

        if (requesterDates.Length != targetDates.Length)
        {
            return Results.BadRequest(new ErrorResponse("Requester and target date lists must have the same number of days."));
        }

        var normalizedType = NormalizeSwapRequestType(request.RequestType);
        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return Results.BadRequest(new ErrorResponse("Request type is invalid."));
        }

        if (string.IsNullOrWhiteSpace(request.Comments))
        {
            return Results.BadRequest(new ErrorResponse("Comments are required."));
        }

        var minDate = ResolveWeekStart(requesterDates.Concat(targetDates).Min());
        var maxDate = ResolveWeekStart(requesterDates.Concat(targetDates).Max()).AddDays(6);
        var overrides = (await _users.GetScheduleOverridesAsync(minDate, maxDate)).ToArray();
        var overrideMap = overrides.ToDictionary(item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}", item => item, StringComparer.OrdinalIgnoreCase);

        var pairs = new List<SwapPairSnapshot>();
        for (var index = 0; index < requesterDates.Length; index++)
        {
            var requesterOfferedDayOffDate = requesterDates[index];
            var targetRequestedDayOffDate = targetDates[index];

            var requesterCellOnTargetDayOffDate = ResolveCalendarCellForDate(requester, targetRequestedDayOffDate, overrideMap);
            var targetCellOnTargetDayOffDate = ResolveCalendarCellForDate(target, targetRequestedDayOffDate, overrideMap);
            var requesterCellOnOfferedDayOffDate = ResolveCalendarCellForDate(requester, requesterOfferedDayOffDate, overrideMap);
            var targetCellOnOfferedDayOffDate = ResolveCalendarCellForDate(target, requesterOfferedDayOffDate, overrideMap);

            if (!IsWorkingCell(requesterCellOnTargetDayOffDate))
            {
                return Results.BadRequest(new ErrorResponse($"Requester must be working on {targetRequestedDayOffDate:yyyy-MM-dd} to take the coworker's day off."));
            }

            if (targetCellOnTargetDayOffDate.Type is not "dayOff")
            {
                return Results.BadRequest(new ErrorResponse($"Selected employee does not have a day off on {targetRequestedDayOffDate:yyyy-MM-dd}."));
            }

            if (requesterCellOnOfferedDayOffDate.Type is not "dayOff")
            {
                return Results.BadRequest(new ErrorResponse($"Requester must offer one of their day off dates on {requesterOfferedDayOffDate:yyyy-MM-dd}."));
            }

            if (!IsWorkingCell(targetCellOnOfferedDayOffDate))
            {
                return Results.BadRequest(new ErrorResponse($"Selected employee must be working on {requesterOfferedDayOffDate:yyyy-MM-dd} to receive the offered day off."));
            }

            pairs.Add(new SwapPairSnapshot
            {
                RequesterCurrent = ToScheduleSnapshot(requester, targetRequestedDayOffDate, requesterCellOnTargetDayOffDate),
                TargetCurrent = ToScheduleSnapshot(target, targetRequestedDayOffDate, targetCellOnTargetDayOffDate),
                RequesterResult = ToScheduleSnapshot(requester, targetRequestedDayOffDate, targetCellOnTargetDayOffDate),
                TargetResult = ToScheduleSnapshot(target, targetRequestedDayOffDate, requesterCellOnTargetDayOffDate)
            });

            pairs.Add(new SwapPairSnapshot
            {
                RequesterCurrent = ToScheduleSnapshot(requester, requesterOfferedDayOffDate, requesterCellOnOfferedDayOffDate),
                TargetCurrent = ToScheduleSnapshot(target, requesterOfferedDayOffDate, targetCellOnOfferedDayOffDate),
                RequesterResult = ToScheduleSnapshot(requester, requesterOfferedDayOffDate, targetCellOnOfferedDayOffDate),
                TargetResult = ToScheduleSnapshot(target, requesterOfferedDayOffDate, requesterCellOnOfferedDayOffDate)
            });
        }

        var weeklyHours = BuildSwapWeeklyHours(requester, target, pairs, overrideMap);

        var nowUtc = DateTime.UtcNow;
        var model = new SwapRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requester.Id,
            RequestedByEmail = requester.Email,
            RequestedByDisplayName = requester.DisplayName ?? requester.Email,
            RequestedByRole = requester.Role,
            TargetUserId = target.Id,
            TargetUserEmail = target.Email,
            TargetUserDisplayName = target.DisplayName ?? target.Email,
            TargetUserRole = target.Role,
            SwapDate = targetDates[0],
            RequestedDatesJson = SerializeDateList(requesterDates),
            TargetDatesJson = SerializeDateList(targetDates),
            PairingsJson = SerializePairs(pairs),
            RequestType = normalizedType,
            Comments = string.IsNullOrWhiteSpace(request.Comments) ? null : request.Comments.Trim(),
            WeeklyHoursJson = SerializeWeeklyHours(weeklyHours),
            Status = "pending",
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        await _users.CreateSwapRequestAsync(model);

        var reviewers = (await _users.GetAllAsync())
            .Where(user => user.IsActive && IsInCallerCompanyScope(user, requester) && RoleHelpers.CanReviewPto(user.Role))
            .Select(user => user.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var reviewer in reviewers)
        {
            await _emailService.SendSwapApprovalEmailAsync(
                reviewer,
                reviewer,
                requester.DisplayName ?? requester.Email,
                requester.Email,
                normalizedType,
                BuildEmailSummaryLines(pairs),
                model.Comments,
                ApiHelpers.BuildSwapReviewLink(_options.FrontendBaseUrl, model.Id));
        }

        await _emailService.SendSwapRequestSubmittedEmailAsync(
            requester.Email,
            requester.DisplayName ?? requester.Email,
            target.DisplayName ?? target.Email,
            target.Email,
            normalizedType,
            BuildEmailSummaryLines(pairs),
            model.Comments);

        return Results.Ok(ToSwapRequestResponse(model));
    }

    public async Task<IResult> GetSwapRequestAsync(HttpContext httpContext, Guid requestId)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || callerContext.Role < 0)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var request = await _users.GetSwapRequestAsync(requestId);
        if (request is null)
        {
            return Results.NotFound(new ErrorResponse("Swap request not found."));
        }

        if (RoleHelpers.IsEmployeeLike(callerContext.Role) &&
            !string.Equals(request.RequestedByEmail, httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.TargetUserEmail, httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value, StringComparison.OrdinalIgnoreCase) &&
            !RoleHelpers.IsEmployeeLike(request.RequestedByRole))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!RoleHelpers.IsEmployeeLike(callerContext.Role) &&
            !await IsSwapInCallerCompanyScopeAsync(callerContext, request))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Ok(ToSwapRequestResponse(request));
    }

    public async Task<IResult> GetSwapRequestsAsync(HttpContext httpContext, HttpRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var status = request.Query["status"].FirstOrDefault();
        var takeQuery = request.Query["take"].FirstOrDefault();
        var take = int.TryParse(takeQuery, out var parsedTake) ? parsedTake : 200;
        var requests = (await _users.GetSwapRequestsAsync(status, take)).ToArray();

        var callerEmail = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.Trim() ?? string.Empty;
        IEnumerable<SwapRequest> visible = RoleHelpers.IsEmployeeLike(callerContext.Role)
            ? requests.Where(item =>
                string.Equals(item.RequestedByEmail.Trim(), callerEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.TargetUserEmail.Trim(), callerEmail, StringComparison.OrdinalIgnoreCase))
            : requests;

        if (!RoleHelpers.IsEmployeeLike(callerContext.Role))
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
            visible = visible.Where(item =>
                visibleUserIds.Contains(item.RequestedByUserId) &&
                visibleUserIds.Contains(item.TargetUserId));
        }

        return Results.Ok(visible.Select(ToSwapRequestResponse));
    }

    public Task<IResult> ApproveSwapRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review) =>
        ReviewSwapRequestAsync(httpContext, requestId, "approved", review);

    public Task<IResult> DenySwapRequestAsync(HttpContext httpContext, Guid requestId, ReviewRequest review) =>
        ReviewSwapRequestAsync(httpContext, requestId, "denied", review);

    public async Task<IResult> CancelSwapRequestAsync(HttpContext httpContext, Guid requestId)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        if (string.IsNullOrWhiteSpace(actor.UpdatedByEmail))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var request = await _users.GetSwapRequestAsync(requestId);
        if (request is null)
        {
            return Results.NotFound(new ErrorResponse("Swap request not found."));
        }

        var status = request.Status.Trim().ToLowerInvariant();
        if (status is not "pending" and not "approved")
        {
            return Results.BadRequest(new ErrorResponse("Only pending or approved swap requests can be canceled."));
        }

        var actorEmail = actor.UpdatedByEmail.Trim();
        var isRequester = string.Equals(request.RequestedByEmail.Trim(), actorEmail, StringComparison.OrdinalIgnoreCase);
        var canManage = RoleHelpers.IsAdmin(callerContext.Role) ||
                        (RoleHelpers.IsManager(callerContext.Role) &&
                         !RoleHelpers.IsAdmin(request.RequestedByRole) &&
                         !RoleHelpers.IsAdmin(request.TargetUserRole));
        if (canManage && !await IsSwapInCallerCompanyScopeAsync(callerContext, request))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (status == "pending")
        {
            if (!isRequester && !canManage)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
        }
        else
        {
            if (!canManage)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var pairDates = DeserializePairs(request.PairingsJson)
                .SelectMany(pair => new[] { pair.RequesterCurrent.Date, pair.TargetCurrent.Date })
                .Select(value => DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToArray();

            if (pairDates.Any(date => date.Date <= DateTime.UtcNow.Date))
            {
                return Results.BadRequest(new ErrorResponse("Approved swaps can only be canceled before the swap date arrives."));
            }
        }

        if (status == "approved" && request.AppliedGroupId.HasValue)
        {
            var groupId = request.AppliedGroupId.Value;
            await _users.RemoveScheduleOverridesByGroupAsync(request.RequestedByUserId, groupId);
            await _users.RemoveScheduleOverridesByGroupAsync(request.TargetUserId, groupId);

            var impactedWeeks = DeserializePairs(request.PairingsJson)
                .SelectMany(pair => new[]
                {
                    DateTime.ParseExact(pair.RequesterCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTime.ParseExact(pair.TargetCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                })
                .Select(ResolveWeekStart)
                .Distinct()
                .ToArray();

            foreach (var impactedWeek in impactedWeeks)
            {
                await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
            }

            await ApiHelpers.PublishScheduleEventAsync(
                _users,
                _hub,
                "swap_canceled",
                request.RequestedByUserId,
                request.RequestedByEmail,
                actor,
                JsonSerializer.Serialize(new { requestId = request.Id, role = request.RequestedByRole, appliedGroupId = groupId }));

            await ApiHelpers.PublishScheduleEventAsync(
                _users,
                _hub,
                "swap_canceled",
                request.TargetUserId,
                request.TargetUserEmail,
                actor,
                JsonSerializer.Serialize(new { requestId = request.Id, role = request.TargetUserRole, appliedGroupId = groupId }));
        }

        await _users.UpdateSwapRequestStatusAsync(requestId, "canceled", actor.UpdatedByEmail, actor.UpdatedByName, callerContext.Role, null);
        var updated = await _users.GetSwapRequestAsync(requestId) ?? request;
        return Results.Ok(ToSwapRequestResponse(updated));
    }

    public async Task<IResult> GetSwapCandidatesAsync(HttpContext httpContext, HttpRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.IsKnownRole(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        if (string.IsNullOrWhiteSpace(actor.UpdatedByEmail))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var requester = await _users.GetByEmailAsync(actor.UpdatedByEmail);
        if (requester is null || !requester.IsActive)
        {
            return Results.NotFound(new ErrorResponse("Requester not found."));
        }

        var targetDates = ParseDistinctDates(request.Query["targetDates"].ToArray());
        if (targetDates.Length == 0)
        {
            return Results.BadRequest(new ErrorResponse("At least one target date is required."));
        }

        var minDate = targetDates.Min();
        var maxDate = targetDates.Max();
        var overrides = (await _users.GetScheduleOverridesAsync(minDate, maxDate)).ToArray();
        var overrideMap = overrides.ToDictionary(item => $"{item.UserId:N}|{item.OverrideDate:yyyy-MM-dd}", item => item, StringComparer.OrdinalIgnoreCase);

        var candidates = (await _users.GetAllAsync())
            .Where(user =>
                user.IsActive &&
                IsInCallerCompanyScope(requester, user) &&
                user.Role == requester.Role &&
                user.Id != requester.Id &&
                targetDates.All(date =>
                {
                    var requesterCell = ResolveCalendarCellForDate(requester, date, overrideMap);
                    var candidateCell = ResolveCalendarCellForDate(user, date, overrideMap);
                    return IsWorkingCell(requesterCell) && candidateCell.Type == "dayOff";
                }))
            .OrderBy(user => user.DisplayName ?? user.Email, StringComparer.OrdinalIgnoreCase)
            .Select(user =>
            {
                var firstCell = ResolveCalendarCellForDate(user, targetDates[0], overrideMap);
                return new SwapCandidateResponse
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName ?? user.Email,
                    Email = user.Email,
                    ShiftTime = firstCell.ShiftTime,
                    ShiftLabel = firstCell.Label
                };
            })
            .ToArray();

        return Results.Ok(candidates);
    }

    private async Task<IResult> ReviewSwapRequestAsync(HttpContext httpContext, Guid requestId, string status, ReviewRequest review)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanReviewPto(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var reviewComments = review.Comments?.Trim();
        if (string.IsNullOrWhiteSpace(reviewComments))
        {
            return Results.BadRequest(new ErrorResponse("A review comment is required."));
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        if (string.IsNullOrWhiteSpace(actor.UpdatedByEmail))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var request = await _users.GetSwapRequestAsync(requestId);
        if (request is null)
        {
            return Results.NotFound(new ErrorResponse("Swap request not found."));
        }

        if (!string.Equals(request.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ErrorResponse("This swap request has already been reviewed."));
        }

        var requesterUser = await _users.GetByIdAsync(request.RequestedByUserId);
        var targetUser = await _users.GetByIdAsync(request.TargetUserId);
        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (requesterUser is null || targetUser is null || callerUser is null ||
            !IsInCallerCompanyScope(callerUser, requesterUser) || !IsInCallerCompanyScope(callerUser, targetUser))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            httpContext.User,
            new PtoReviewResource(requesterUser.Role, request.RequestedByRole),
            "CanReviewPto");
        if (!authorizationResult.Succeeded) return Results.StatusCode(StatusCodes.Status403Forbidden);

        var pairs = DeserializePairs(request.PairingsJson);
        Guid? appliedGroupId = null;

        if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            var impactedWeeks = pairs
                .SelectMany(pair => new[]
                {
                    DateTime.ParseExact(pair.RequesterCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTime.ParseExact(pair.TargetCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                })
                .Select(ResolveWeekStart)
                .Distinct()
                .ToArray();

            appliedGroupId = Guid.NewGuid();
            foreach (var pair in pairs)
            {
                var requesterDate = DateTime.ParseExact(pair.RequesterCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var targetDate = DateTime.ParseExact(pair.TargetCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                await _users.UpsertScheduleOverrideAsync(BuildSwapOverride(request.RequestedByUserId, requesterDate, pair.RequesterResult, request.Comments, appliedGroupId.Value));
                await _users.UpsertScheduleOverrideAsync(BuildSwapOverride(request.TargetUserId, targetDate, pair.TargetResult, request.Comments, appliedGroupId.Value));
            }

            foreach (var impactedWeek in impactedWeeks)
            {
                await RebuildWeekSnapshotAsync(_users, _coverageRules, impactedWeek);
            }
        }

        await _users.UpdateSwapRequestStatusAsync(requestId, status, actor.UpdatedByEmail, actor.UpdatedByName, callerContext.Role, appliedGroupId, reviewComments);
        var updated = await _users.GetSwapRequestAsync(requestId) ?? request;

        if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            await _emailService.SendSwapApprovedEmailAsync(
                request.RequestedByEmail,
                request.RequestedByDisplayName,
                string.IsNullOrWhiteSpace(actor.UpdatedByName) ? actor.UpdatedByEmail : actor.UpdatedByName,
                actor.UpdatedByEmail,
                request.RequestType,
                BuildEmailSummaryLines(pairs),
                reviewComments);

            await _emailService.SendSwapApprovedEmailAsync(
                request.TargetUserEmail,
                request.TargetUserDisplayName,
                string.IsNullOrWhiteSpace(actor.UpdatedByName) ? actor.UpdatedByEmail : actor.UpdatedByName,
                actor.UpdatedByEmail,
                request.RequestType,
                BuildEmailSummaryLines(pairs),
                reviewComments);

            var notifyRecipients = (await _users.GetAllAsync())
                .Where(user =>
                    user.IsActive &&
                    requesterUser is not null &&
                    targetUser is not null &&
                    IsInCallerCompanyScope(user, requesterUser) &&
                    IsInCallerCompanyScope(user, targetUser) &&
                    (RoleHelpers.IsAdmin(request.RequestedByRole)
                        ? RoleHelpers.IsAdmin(user.Role)
                        : RoleHelpers.CanReviewPto(user.Role)))
                .Select(user => user.Email)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await _emailService.SendSwapApprovedSummaryEmailAsync(
                notifyRecipients,
                request.RequestedByDisplayName,
                request.RequestedByEmail,
                request.TargetUserDisplayName,
                request.TargetUserEmail,
                request.RequestType,
                BuildEmailSummaryLines(pairs),
                reviewComments);

            await ApiHelpers.PublishScheduleEventAsync(
                _users,
                _hub,
                "swap_approved",
                request.RequestedByUserId,
                request.RequestedByEmail,
                actor,
                JsonSerializer.Serialize(new { requestId = request.Id, role = request.RequestedByRole, appliedGroupId }));

            await ApiHelpers.PublishScheduleEventAsync(
                _users,
                _hub,
                "swap_approved",
                request.TargetUserId,
                request.TargetUserEmail,
                actor,
                JsonSerializer.Serialize(new { requestId = request.Id, role = request.TargetUserRole, appliedGroupId }));
        }
        else
        {
            await _emailService.SendSwapDeniedEmailAsync(
                request.RequestedByEmail,
                request.RequestedByDisplayName,
                request.RequestType,
                BuildEmailSummaryLines(pairs),
                string.IsNullOrWhiteSpace(actor.UpdatedByName) ? actor.UpdatedByEmail : actor.UpdatedByName,
                reviewComments);
        }

        return Results.Ok(ToSwapRequestResponse(updated));
    }

    private static SwapWeeklyHoursSnapshot[] BuildSwapWeeklyHours(
        User requester,
        User target,
        IEnumerable<SwapPairSnapshot> pairs,
        IReadOnlyDictionary<string, UserScheduleOverride> overrideMap)
    {
        return pairs
            .Select(pair => DateTime.ParseExact(pair.RequesterCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Select(ResolveWeekStart)
            .Distinct()
            .OrderBy(date => date)
            .Select(weekStart =>
            {
                var days = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset)).ToArray();
                var requesterHours = BuildCalendarRow(requester, days, overrideMap).Cells.Sum(cell => cell.DurationHours);
                var targetHours = BuildCalendarRow(target, days, overrideMap).Cells.Sum(cell => cell.DurationHours);
                foreach (var pair in pairs.Where(pair => ResolveWeekStart(DateTime.ParseExact(pair.RequesterCurrent.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)) == weekStart))
                {
                    requesterHours += pair.RequesterResult.DurationHours - pair.RequesterCurrent.DurationHours;
                    targetHours += pair.TargetResult.DurationHours - pair.TargetCurrent.DurationHours;
                }

                return new SwapWeeklyHoursSnapshot
                {
                    WeekStart = weekStart.ToString("yyyy-MM-dd"),
                    RequesterHours = Math.Round(requesterHours, 2),
                    TargetHours = Math.Round(targetHours, 2)
                };
            })
            .ToArray();
    }

    private async Task<bool> IsSwapInCallerCompanyScopeAsync(CallerContext callerContext, SwapRequest request)
    {
        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return false;
        }

        var requester = await _users.GetByIdAsync(request.RequestedByUserId);
        var target = await _users.GetByIdAsync(request.TargetUserId);
        return requester is not null &&
               target is not null &&
               IsInCallerCompanyScope(callerUser, requester) &&
               IsInCallerCompanyScope(callerUser, target);
    }

    private static DateTime[] ParseDistinctDates(IEnumerable<string?>? values)
    {
        var results = new List<DateTime>();
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (!DateTime.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                continue;
            }

            if (!results.Contains(parsed.Date))
            {
                results.Add(parsed.Date);
            }
        }

        return results.OrderBy(item => item).ToArray();
    }

    private static SwapScheduleSnapshot ToScheduleSnapshot(User owner, DateTime date, CalendarCell cell) => new()
    {
        OwnerName = owner.DisplayName ?? owner.Email,
        OwnerEmail = owner.Email,
        Date = date.ToString("yyyy-MM-dd"),
        Label = cell.Label,
        ShiftTime = cell.ShiftTime,
        DurationHours = cell.DurationHours,
        Type = cell.Type
    };

    private static UserScheduleOverride BuildSwapOverride(Guid userId, DateTime date, SwapScheduleSnapshot result, string? comments, Guid groupId)
    {
        var (start, end) = ExtractTimeBounds(result.Label);
        return new UserScheduleOverride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OverrideDate = date.Date,
            GroupId = groupId,
            EntryType = result.Type,
            RequestType = "swap_shift",
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            StartTime = start,
            EndTime = end,
            Label = result.Label,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static (string? Start, string? End) ExtractTimeBounds(string label)
    {
        var parts = label.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        return (null, null);
    }

}
