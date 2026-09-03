using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

public static class ApiHelpers
{
    public static string BuildResetLink(string frontendBaseUrl, string resetCode)
    {
        var baseUrl = frontendBaseUrl?.TrimEnd('/') ?? string.Empty;
        var encodedCode = Uri.EscapeDataString(resetCode);
        return $"{baseUrl}/reset?code={encodedCode}";
    }

    public static string BuildPtoReviewLink(string frontendBaseUrl, Guid requestId)
    {
        var baseUrl = frontendBaseUrl?.TrimEnd('/') ?? string.Empty;
        return $"{baseUrl}/pto-review?requestId={requestId:D}";
    }

    public static string BuildSwapReviewLink(string frontendBaseUrl, Guid requestId)
    {
        var baseUrl = frontendBaseUrl?.TrimEnd('/') ?? string.Empty;
        return $"{baseUrl}/swap-review?requestId={requestId:D}";
    }

    public static string[] PermissionsForRole(int role) => role switch
    {
        RoleHelpers.Admin => new[]
        {
            "viewSchedule",
            "requestLeave",
            "requestLeaveForOthers",
            "approveLeaves",
            "assignShifts",
            "overrideCapacity",
            "manageUsers",
            "configureRules"
        },
        RoleHelpers.Manager => new[]
        {
            "viewSchedule",
            "requestLeave",
            "requestLeaveForOthers",
            "approveLeaves",
            "assignShifts",
            "overrideCapacity",
            "manageUsers"
        },
        RoleHelpers.TeamLeader => new[]
        {
            "viewSchedule",
            "requestLeave",
            "viewCoverage"
        },
        _ => new[]
        {
            "viewSchedule",
            "requestLeave"
        }
    };

    public static string? FindDuplicateDay(IEnumerable<ScheduleBlockRequest>? blocks)
    {
        if (blocks is null) return null;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in blocks)
        {
            if (b.Days is null) continue;
            foreach (var d in b.Days)
            {
                if (!used.Add(d)) return $"Day {d} is selected in multiple blocks.";
            }
        }
        return null;
    }

    public static string? ValidateSchedulePeriods(IEnumerable<SchedulePeriodRequest>? schedulePeriods)
    {
        if (schedulePeriods is null || !schedulePeriods.Any()) return "Schedule Periods";
        foreach (var period in schedulePeriods)
        {
            if (string.IsNullOrWhiteSpace(period.EffectiveFrom)) return "Effective From";
            if (string.IsNullOrWhiteSpace(period.ShiftTime)) return "Shift Time";
            if (period.ScheduleBlocks is null || !period.ScheduleBlocks.Any()) return "Schedule Blocks";
            foreach (var block in period.ScheduleBlocks)
            {
                if (string.IsNullOrWhiteSpace(block.Start)) return "Start (Schedule)";
                if (string.IsNullOrWhiteSpace(block.End)) return "End (Schedule)";
                if (block.Days is null || block.Days.Length == 0) return "Days (Schedule)";
            }
        }
        return null;
    }

    public static string DayAbbrev(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        DayOfWeek.Sunday => "Sun",
        _ => ""
    };

    public static double TryDurationHours(string start, string end)
    {
        if (TimeSpan.TryParse(start, out var s) && TimeSpan.TryParse(end, out var e))
        {
            // If end is past midnight, assume next day
            if (e < s) e = e.Add(TimeSpan.FromHours(24));
            return Math.Round((e - s).TotalHours, 1);
        }
        return 0;
    }

    public static ActorContext ExtractActor(HttpContext httpContext, int callerRole)
    {
        var updatedByEmail = httpContext.User.FindFirstValue(ClaimTypes.Email)?.Trim() ?? string.Empty;
        var updatedByName = httpContext.User.FindFirstValue(ClaimTypes.Name)?.Trim() ?? string.Empty;
        var updatedByUserIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
        Guid? updatedByUserId = null;
        if (Guid.TryParse(updatedByUserIdValue, out var parsed)) updatedByUserId = parsed;

        return new ActorContext
        {
            UpdatedByEmail = updatedByEmail,
            UpdatedByName = updatedByName,
            UpdatedByUserId = updatedByUserId,
            UpdatedByRole = callerRole
        };
    }

    public static async Task PublishScheduleEventAsync(
        IUserRepository users,
        IHubContext<ScheduleHub> hub,
        string action,
        Guid? employeeId,
        string employeeEmail,
        ActorContext actor,
        string payloadJson)
    {
        var now = DateTime.UtcNow;
        var evt = new ScheduleEvent
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            EmployeeEmail = employeeEmail ?? string.Empty,
            Action = action,
            UpdatedByUserId = actor.UpdatedByUserId,
            UpdatedByEmail = actor.UpdatedByEmail,
            UpdatedByName = actor.UpdatedByName,
            UpdatedByRole = actor.UpdatedByRole,
            OccurredAtUtc = now,
            PayloadJson = payloadJson ?? "{}"
        };

        await users.CreateScheduleEventAsync(evt);

        await hub.Clients.All.SendAsync(ScheduleRealtime.EventName, new ScheduleUpdatedEvent
        {
            EmployeeId = employeeId?.ToString() ?? string.Empty,
            EmployeeEmail = employeeEmail ?? string.Empty,
            Action = action,
            UpdatedByUserId = actor.UpdatedByUserId?.ToString() ?? string.Empty,
            UpdatedByEmail = actor.UpdatedByEmail,
            UpdatedByName = actor.UpdatedByName,
            UpdatedByRole = actor.UpdatedByRole,
            OccurredAtUtc = now.ToString("O")
        });
    }
}

public record ActorContext
{
    public Guid? UpdatedByUserId { get; init; }
    public string UpdatedByEmail { get; init; } = string.Empty;
    public string UpdatedByName { get; init; } = string.Empty;
    public int UpdatedByRole { get; init; }
}

public record LoginRequest(string Email, string Password);
public record EntraLoginRequest(string IdToken);
public record EntraCodeLoginRequest(string Code, string CodeVerifier, string RedirectUri);

public record AuthResponse(string Email, string DisplayName, int Role, string[] Permissions, bool IsSystemHidden, string Company, string[] Companies);

public record ErrorResponse(string Message);

public record CompanyResponse(string Name, bool IsActive);
public record UpsertCompanyRequest(string Name);
public record SetCompanyStatusRequest(string Name, bool IsActive);
public record RenameCompanyRequest(string CurrentName, string NewName);
public record CompanyOperationResponse(string CompanyName, string Name, bool IsActive);
public record UpsertCompanyOperationRequest(string CompanyName, string Name);
public record SetCompanyOperationStatusRequest(string CompanyName, string Name, bool IsActive);
public record RenameCompanyOperationRequest(string CompanyName, string CurrentName, string NewName);
public record CoverageRuleDayRequest(string DayOfWeek, int ExpectedCoverage, int GreenThreshold, int YellowThreshold, bool IsActive = true);
public record UpsertCoverageRulesRequest(string CompanyName, string? OperationName, string? CalculationScope, CoverageRuleDayRequest[] Rules);

public record CoverageRuleResponse
{
    public string CompanyName { get; init; } = string.Empty;
    public string? OperationName { get; init; }
    public string DayOfWeek { get; init; } = string.Empty;
    public int ExpectedCoverage { get; init; }
    public int GreenThreshold { get; init; }
    public int YellowThreshold { get; init; }
    public string CalculationScope { get; init; } = "operation";
    public bool IsActive { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
    public string UpdatedAtUtc { get; init; } = string.Empty;
}

public record ResetPasswordRequest(string Email, string NewPassword);

public record ForgotPasswordRequest(string Email);
public record ResetPasswordCodeExchangeRequest(string Code);
public record ResetPasswordCodeExchangeResponse(string Email, string ExchangeToken);
public record ResetPasswordCompleteRequest(string Email, string ExchangeToken, string NewPassword);

public record ResetPasswordWithTokenRequest(string Email, string Token, string NewPassword);

public record ForceChangePasswordRequest(string Email, string? Token, string? CurrentPassword, string NewPassword);

public record ScheduleBlockDto(string Start, string End, string[] Days);
public record SchedulePeriodDto(string EffectiveFrom, string? EffectiveTo, string ShiftTime, ScheduleBlockDto[] ScheduleBlocks, bool IsRepeating = false);
public record SchedulePeriodRequest(string EffectiveFrom, string? EffectiveTo, string ShiftTime, IEnumerable<ScheduleBlockRequest>? ScheduleBlocks, bool IsRepeating = false);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    int Role,
    string Location,
    string Company,
    string Operation,
    IEnumerable<SchedulePeriodRequest>? SchedulePeriods,
    string[]? Companies = null,
    bool IsSystemHidden = false)
{
    public string? GetMissingField()
    {
        if (string.IsNullOrWhiteSpace(FirstName)) return "First Name";
        if (string.IsNullOrWhiteSpace(LastName)) return "Last Name";
        if (string.IsNullOrWhiteSpace(Email)) return "Email";
        if (string.IsNullOrWhiteSpace(Password)) return "Password";
        if (string.IsNullOrWhiteSpace(Location)) return "Location";
        if (!IsSystemHidden && string.IsNullOrWhiteSpace(Company)) return "Company";
        if (string.IsNullOrWhiteSpace(Operation)) return "Operation";
        return IsSystemHidden && (SchedulePeriods is null || !SchedulePeriods.Any())
            ? null
            : ApiHelpers.ValidateSchedulePeriods(SchedulePeriods);
    }
}

public record ScheduleBlockRequest(string Start, string End, string[] Days);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    int Role,
    string Location,
    string Company,
    string Operation,
    IEnumerable<SchedulePeriodRequest>? SchedulePeriods,
    string[]? Companies = null,
    bool? IsSystemHidden = null)
{
    public string? GetMissingField(bool isSystemHidden)
    {
        if (string.IsNullOrWhiteSpace(FirstName)) return "First Name";
        if (string.IsNullOrWhiteSpace(LastName)) return "Last Name";
        if (string.IsNullOrWhiteSpace(Location)) return "Location";
        if (!isSystemHidden && string.IsNullOrWhiteSpace(Company)) return "Company";
        if (string.IsNullOrWhiteSpace(Operation)) return "Operation";
        return isSystemHidden && (SchedulePeriods is null || !SchedulePeriods.Any())
            ? null
            : ApiHelpers.ValidateSchedulePeriods(SchedulePeriods);
    }
}

public record BulkUserUploadError(int Row, string Column, string Email, string Message);

public record BulkUserUploadResponse
{
    public string Message { get; init; } = string.Empty;
    public int Created { get; init; }
    public int Updated { get; init; }
    public int RowsProcessed { get; init; }
    public BulkUserUploadError[] Errors { get; init; } = Array.Empty<BulkUserUploadError>();
}

public record UpsertPtoRequest(
    Guid UserId,
    string StartDate,
    int NumberOfDays,
    string RequestType,
    string? Comments,
    Guid? ExistingGroupId,
    string? EmployeeFilter = null,
    string? RoleFilter = null,
    string? ShiftFilter = null,
    string? OperationFilter = null,
    string? CompanyFilter = null);

public record ReviewRequest(string? Comments);

public record UpsertDailyScheduleRequest(
    Guid UserId,
    string Date,
    string StartTime,
    string EndTime,
    string? Comments);

public record CoverageImpactWarningResponse
{
    public string Date { get; init; } = string.Empty;
    public int RequiredAgents { get; init; }
    public int CurrentWorkingAgents { get; init; }
    public int ProjectedWorkingAgents { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record PtoCoveragePreviewResponse
{
    public bool HasImpact { get; init; }
    public CoverageImpactWarningResponse[] Warnings { get; init; } = Array.Empty<CoverageImpactWarningResponse>();
}

public record CreateSwapRequest(
    Guid TargetUserId,
    string[] RequestedDates,
    string[] TargetDates,
    string RequestType,
    string? Comments);

public record SwapCandidateResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ShiftTime { get; init; } = string.Empty;
    public string ShiftLabel { get; init; } = string.Empty;
}

public record SwapRequestResponse
{
    public Guid Id { get; init; }
    public Guid RequestedByUserId { get; init; }
    public string RequestedByEmail { get; init; } = string.Empty;
    public string RequestedByDisplayName { get; init; } = string.Empty;
    public int RequestedByRole { get; init; }
    public Guid TargetUserId { get; init; }
    public string TargetUserEmail { get; init; } = string.Empty;
    public string TargetUserDisplayName { get; init; } = string.Empty;
    public int TargetUserRole { get; init; }
    public string SwapDate { get; init; } = string.Empty;
    public string[] RequestedDates { get; init; } = Array.Empty<string>();
    public string[] TargetDates { get; init; } = Array.Empty<string>();
    public Guid? AppliedGroupId { get; init; }
    public SwapPairResponse[] Pairs { get; init; } = Array.Empty<SwapPairResponse>();
    public string RequestType { get; init; } = string.Empty;
    public string? Comments { get; init; }
    public string? ReviewComments { get; init; }
    public SwapWeeklyHoursResponse[] WeeklyHours { get; init; } = Array.Empty<SwapWeeklyHoursResponse>();
    public bool ExceedsWeeklyHoursLimit { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ReviewedByEmail { get; init; }
    public string? ReviewedByName { get; init; }
    public int? ReviewedByRole { get; init; }
    public string? ReviewedAtUtc { get; init; }
    public string CreatedAtUtc { get; init; } = string.Empty;
}

public record SwapWeeklyHoursResponse
{
    public string WeekStart { get; init; } = string.Empty;
    public double RequesterHours { get; init; }
    public double TargetHours { get; init; }
    public double LimitHours { get; init; } = 45;
}

public record SwapScheduleEntryResponse
{
    public string Date { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string ShiftTime { get; init; } = string.Empty;
    public double DurationHours { get; init; }
    public string Type { get; init; } = string.Empty;
}

public record SwapPairResponse
{
    public SwapScheduleEntryResponse RequesterCurrent { get; init; } = new();
    public SwapScheduleEntryResponse TargetCurrent { get; init; } = new();
    public SwapScheduleEntryResponse RequesterResult { get; init; } = new();
    public SwapScheduleEntryResponse TargetResult { get; init; } = new();
}

public record PtoRequestResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string UserEmail { get; init; } = string.Empty;
    public string UserDisplayName { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public int NumberOfDays { get; init; }
    public string StartDate { get; init; } = string.Empty;
    public string EndDate { get; init; } = string.Empty;
    public string? Comments { get; init; }
    public string? ReviewComments { get; init; }
    public Guid? OverrideGroupId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string RequestedByEmail { get; init; } = string.Empty;
    public string RequestedByName { get; init; } = string.Empty;
    public int RequestedByRole { get; init; }
    public string? ReviewedByEmail { get; init; }
    public string? ReviewedByName { get; init; }
    public int? ReviewedByRole { get; init; }
    public string? ReviewedAtUtc { get; init; }
    public string CreatedAtUtc { get; init; } = string.Empty;
}

public record RequestExportJobResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string? ErrorMessage { get; init; }
    public string CreatedAtUtc { get; init; } = string.Empty;
    public string? StartedAtUtc { get; init; }
    public string? CompletedAtUtc { get; init; }
    public string? ExpiresAtUtc { get; init; }
    public string? DownloadUrl { get; init; }
}

public record CalendarResponse
{
    public string WeekStart { get; init; } = string.Empty;
    public string WeekEnd { get; init; } = string.Empty;
    public DayDescriptor[] Days { get; init; } = Array.Empty<DayDescriptor>();
    public CoverageSummary[] Coverage { get; init; } = Array.Empty<CoverageSummary>();
    public CalendarRow[] Items { get; init; } = Array.Empty<CalendarRow>();
}

public record CoverageSummary
{
    public string Date { get; init; } = string.Empty;
    public string DayCode { get; init; } = string.Empty;
    public int ExpectedCoverage { get; init; }
    public double Coverage { get; init; }
    public int TotalAgents { get; init; }
    public string StatusColor { get; init; } = "red"; // red, yellow, green
}

public record DayDescriptor
{
    public string Date { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public record CalendarRow
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int Role { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string ShiftTime { get; init; } = string.Empty;
    public CalendarCell[] Cells { get; init; } = Array.Empty<CalendarCell>();
}

public record CalendarCell
{
    public string Date { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public double DurationHours { get; init; }
    public string Type { get; init; } = "dayOff"; // dayOff, shiftMorning, shiftLate, leave
    public string ShiftTime { get; init; } = string.Empty;
    public string? PtoGroupId { get; init; }
    public string? PtoRequestType { get; init; }
    public string? PtoComments { get; init; }
    public bool IsPtoStart { get; init; }
    public bool IsDailyScheduleOverride { get; init; }
    public string? ScheduleOverrideComments { get; init; }
}

public static class ScheduleRealtime
{
    public const string EventName = "schedule.updated";
}

public record ScheduleUpdatedEvent
{
    public string EmployeeId { get; init; } = string.Empty;
    public string EmployeeEmail { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // created, updated, deleted
    public string UpdatedByUserId { get; init; } = string.Empty;
    public string UpdatedByEmail { get; init; } = string.Empty;
    public string UpdatedByName { get; init; } = string.Empty;
    public int UpdatedByRole { get; init; }
    public string OccurredAtUtc { get; init; } = string.Empty;
}

public record ScheduleEventResponse
{
    public Guid Id { get; init; }
    public string EmployeeId { get; init; } = string.Empty;
    public string EmployeeEmail { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string UpdatedByUserId { get; init; } = string.Empty;
    public string UpdatedByEmail { get; init; } = string.Empty;
    public string UpdatedByName { get; init; } = string.Empty;
    public int UpdatedByRole { get; init; }
    public string OccurredAtUtc { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
}

public record ReportsOverviewResponse
{
    public string SelectedCompany { get; init; } = string.Empty;
    public string[] AvailableCompanies { get; init; } = Array.Empty<string>();
    public string WeekStart { get; init; } = string.Empty;
    public string WeekEnd { get; init; } = string.Empty;
    public ReportsKpiResponse Kpis { get; init; } = new();
    public ReportCoverageHeatmapPoint[] CoverageHeatmap { get; init; } = Array.Empty<ReportCoverageHeatmapPoint>();
    public ReportCoverageTrendPoint[] CoverageTrend { get; init; } = Array.Empty<ReportCoverageTrendPoint>();
    public ReportCoverageDayPoint[] ExpectedVsActual { get; init; } = Array.Empty<ReportCoverageDayPoint>();
    public ReportMetricPoint[] PtoByStatus { get; init; } = Array.Empty<ReportMetricPoint>();
    public ReportMetricPoint[] PtoByType { get; init; } = Array.Empty<ReportMetricPoint>();
    public ReportHeadcountPoint[] HeadcountByOperation { get; init; } = Array.Empty<ReportHeadcountPoint>();
    public ReportOperationRiskPoint[] TopRiskOperations { get; init; } = Array.Empty<ReportOperationRiskPoint>();
}

public record ReportsKpiResponse
{
    public int TotalActiveEmployees { get; init; }
    public double AverageWeeklyCoverage { get; init; }
    public int RiskDays { get; init; }
    public int PendingPtoRequests { get; init; }
    public int Operations { get; init; }
}

public record ReportMetricPoint(string Label, double Value);
public record ReportCoverageHeatmapPoint(string Operation, string Day, string Date, double Coverage, int ExpectedCoverage, string StatusColor);
public record ReportCoverageTrendPoint(string WeekStart, double AverageCoverage, int RiskDays);
public record ReportCoverageDayPoint(string Day, string Date, int ExpectedCoverage, double Coverage, int TotalAgents);
public record ReportHeadcountPoint(string Operation, int Active, int Inactive);
public record ReportOperationRiskPoint(string Operation, int RiskDays, double AverageCoverage);

public record AssistantQueryRequest(string Message, string? WeekStart);

public record AssistantQueryResponse
{
    public string Intent { get; init; } = "unknown";
    public string Status { get; init; } = "ok";
    public string Message { get; init; } = string.Empty;
    public string WeekStart { get; init; } = string.Empty;
    public AssistantEmployeeResult[] Matches { get; init; } = Array.Empty<AssistantEmployeeResult>();
}

public record AssistantEmployeeResult
{
    public string EmployeeId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public AssistantCalendarFact[] Facts { get; init; } = Array.Empty<AssistantCalendarFact>();
}

public record AssistantCalendarFact
{
    public string Type { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}
