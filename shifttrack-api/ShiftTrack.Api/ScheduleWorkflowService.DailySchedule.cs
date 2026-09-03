using System.Globalization;
using System.Text.Json;
using ShiftTrack.Application;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal sealed partial class ScheduleWorkflowService
{
    public async Task<IResult> UpsertDailyScheduleAsync(HttpContext httpContext, UpsertDailyScheduleRequest request)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanManageUsers(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var target = await _users.GetByIdAsync(request.UserId);
        if (target is null || !target.IsActive)
        {
            return Results.NotFound(new ErrorResponse("Employee not found."));
        }

        var caller = await ResolveCallerUserAsync(callerContext);
        if (caller is null || !IsInCallerCompanyScope(caller, target))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (RoleHelpers.IsManager(callerContext.Role) && !RoleHelpers.CanManagerManageRole(target.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!DateTime.TryParseExact(request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return Results.BadRequest(new ErrorResponse("Date is invalid."));
        }

        if (!TimeOnly.TryParseExact(request.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            !TimeOnly.TryParseExact(request.EndTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return Results.BadRequest(new ErrorResponse("Start time and end time must use HH:mm format."));
        }

        if (string.Equals(request.StartTime, request.EndTime, StringComparison.Ordinal))
        {
            return Results.BadRequest(new ErrorResponse("Start time and end time must be different."));
        }

        var comments = request.Comments?.Trim();
        if (string.IsNullOrWhiteSpace(comments))
        {
            return Results.BadRequest(new ErrorResponse("Comments are required."));
        }

        var existing = (await _users.GetScheduleOverridesAsync(date.Date, date.Date))
            .FirstOrDefault(item => item.UserId == target.Id && item.OverrideDate.Date == date.Date);
        if (existing is not null && IsLeaveOrDayOffOverride(existing.EntryType))
        {
            return Results.BadRequest(new ErrorResponse("A PTO or Day Off request already exists for this date."));
        }

        var actor = ApiHelpers.ExtractActor(httpContext, callerContext.Role);
        var duration = ApiHelpers.TryDurationHours(request.StartTime, request.EndTime);
        var entryType = ResolveDailyShiftType(request.StartTime);
        await _users.UpsertScheduleOverrideAsync(new UserScheduleOverride
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            UserId = target.Id,
            OverrideDate = date.Date,
            EntryType = "daily_schedule",
            RequestType = entryType,
            Comments = comments,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Label = $"{request.StartTime} - {request.EndTime}",
            CreatedAtUtc = DateTime.UtcNow
        });

        await RebuildWeekSnapshotAsync(_users, _coverageRules, ResolveWeekStart(date.Date));
        await ApiHelpers.PublishScheduleEventAsync(
            _users,
            _hub,
            "daily_schedule_updated",
            target.Id,
            target.Email,
            actor,
            JsonSerializer.Serialize(new
            {
                date = date.ToString("yyyy-MM-dd"),
                request.StartTime,
                request.EndTime,
                durationHours = duration,
                comments
            }));

        _ = SendDailyScheduleChangedEmailBestEffortAsync(
            target.Email,
            string.IsNullOrWhiteSpace(target.DisplayName) ? target.Email : target.DisplayName,
            string.IsNullOrWhiteSpace(actor.UpdatedByName) ? actor.UpdatedByEmail : actor.UpdatedByName,
            actor.UpdatedByEmail,
            date.ToString("yyyy-MM-dd"),
            request.StartTime,
            request.EndTime,
            duration,
            comments);

        return Results.Ok(new
        {
            target.Id,
            Date = date.ToString("yyyy-MM-dd"),
            request.StartTime,
            request.EndTime,
            DurationHours = duration,
            Comments = comments
        });
    }

    private static string ResolveDailyShiftType(string startTime) =>
        TimeOnly.TryParseExact(startTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) && start.Hour >= 12
            ? "shiftLate"
            : "shiftMorning";

    private static bool IsLeaveOrDayOffOverride(string? entryType)
    {
        var normalized = (entryType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "pto" or "vacations" or "absence" or "sickleave" or "sick_leave" or
            "maternityleave" or "maternity_leave" or "paternityleave" or "paternity_leave" or
            "birthday" or "holiday" or "familyday" or "family_day" or "fmla" or "unpaidleave" or
            "unpaid_leave" or "dayoff" or "day_off";
    }

    private async Task SendDailyScheduleChangedEmailBestEffortAsync(
        string recipientEmail,
        string recipientName,
        string changedByName,
        string changedByEmail,
        string date,
        string startTime,
        string endTime,
        double durationHours,
        string comments)
    {
        try
        {
            await _emailService.SendDailyScheduleChangedEmailAsync(
                recipientEmail,
                recipientName,
                changedByName,
                changedByEmail,
                date,
                startTime,
                endTime,
                durationHours,
                comments);
        }
        catch
        {
            // Email is intentionally best-effort here. A notification failure must not roll back
            // or block the daily schedule override that was already persisted and published.
        }
    }
}
