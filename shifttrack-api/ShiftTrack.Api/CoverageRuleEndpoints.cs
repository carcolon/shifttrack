using System.Globalization;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class CoverageRuleEndpoints
{
    internal static WebApplication MapCoverageRuleEndpoints(this WebApplication app)
    {
        app.MapGet("/coverage-rules", async Task<IResult> (
            HttpContext httpContext,
            IUserRepository users,
            ICoverageRuleRepository coverageRules,
            string company,
            string? operation = null) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !RoleHelpers.CanViewCoverage(callerUser.Role))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var normalizedCompany = NormalizeRequired(company);
            if (normalizedCompany is null)
            {
                return Results.BadRequest(new ErrorResponse("Company is required."));
            }

            if (!CanAccessCompany(callerUser, normalizedCompany))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var resolved = await coverageRules.ResolveRulesAsync(normalizedCompany, NormalizeOptional(operation));
            return Results.Ok(resolved.Select(ToResponse));
        }).RequireAuthorization();

        app.MapPut("/coverage-rules", async Task<IResult> (
            HttpContext httpContext,
            UpsertCoverageRulesRequest request,
            IUserRepository users,
            ICoverageRuleRepository coverageRules) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !RoleHelpers.IsAdmin(callerUser.Role))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var normalizedCompany = NormalizeRequired(request.CompanyName);
            if (normalizedCompany is null)
            {
                return Results.BadRequest(new ErrorResponse("Company is required."));
            }

            if (!CanAccessCompany(callerUser, normalizedCompany))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var normalizedOperation = NormalizeOptional(request.OperationName);
            var calculationScope = NormalizeCalculationScope(request.CalculationScope);
            var parsedRules = new List<CoverageRule>();
            var seenDays = new HashSet<DayOfWeek>();
            foreach (var item in request.Rules ?? Array.Empty<CoverageRuleDayRequest>())
            {
                if (!TryParseDay(item.DayOfWeek, out var day))
                {
                    return Results.BadRequest(new ErrorResponse($"Invalid day of week: {item.DayOfWeek}."));
                }

                if (!seenDays.Add(day))
                {
                    return Results.BadRequest(new ErrorResponse($"Duplicate rule for {day}."));
                }

                var validationError = ValidatePercentages(item);
                if (validationError is not null)
                {
                    return Results.BadRequest(new ErrorResponse(validationError));
                }

                parsedRules.Add(new CoverageRule
                {
                    Id = Guid.NewGuid(),
                    CompanyName = normalizedCompany,
                    OperationName = normalizedOperation,
                    DayOfWeek = day,
                    ExpectedCoverage = item.ExpectedCoverage,
                    GreenThreshold = item.GreenThreshold,
                    YellowThreshold = item.YellowThreshold,
                    CalculationScope = calculationScope,
                    IsActive = item.IsActive,
                    UpdatedBy = callerUser.Email,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }

            if (parsedRules.Count != 7)
            {
                return Results.BadRequest(new ErrorResponse("Rules must include all seven days of the week."));
            }

            await coverageRules.UpsertRulesAsync(parsedRules);
            var resolved = await coverageRules.ResolveRulesAsync(normalizedCompany, normalizedOperation);
            return Results.Ok(resolved.Select(ToResponse));
        }).RequireAuthorization();

        return app;
    }

    private static string? ValidatePercentages(CoverageRuleDayRequest item)
    {
        if (!IsPercent(item.ExpectedCoverage)) return "Expected Coverage must be between 0 and 100.";
        if (!IsPercent(item.GreenThreshold)) return "Green Threshold must be between 0 and 100.";
        if (!IsPercent(item.YellowThreshold)) return "Yellow Threshold must be between 0 and 100.";
        if (item.GreenThreshold < item.YellowThreshold) return "Green Threshold must be greater than or equal to Yellow Threshold.";
        return null;
    }

    private static bool IsPercent(int value) => value is >= 0 and <= 100;

    private static bool TryParseDay(string value, out DayOfWeek day)
    {
        day = default;
        if (Enum.TryParse(value, ignoreCase: true, out DayOfWeek parsed))
        {
            day = parsed;
            return true;
        }

        var matches = CoverageRuleDefaults.OrderedDays.Where(item =>
            string.Equals(ApiHelpers.DayAbbrev(item), value, StringComparison.OrdinalIgnoreCase));
        var match = matches.Cast<DayOfWeek?>().FirstOrDefault();
        if (!match.HasValue)
        {
            return false;
        }

        day = match.Value;
        return true;
    }

    private static CoverageRuleResponse ToResponse(CoverageRule rule) => new()
    {
        CompanyName = rule.CompanyName,
        OperationName = string.IsNullOrWhiteSpace(rule.OperationName) ? null : rule.OperationName,
        DayOfWeek = rule.DayOfWeek.ToString(),
        ExpectedCoverage = rule.ExpectedCoverage,
        GreenThreshold = rule.GreenThreshold,
        YellowThreshold = rule.YellowThreshold,
        CalculationScope = NormalizeCalculationScope(rule.CalculationScope),
        IsActive = rule.IsActive,
        UpdatedBy = rule.UpdatedBy,
        UpdatedAtUtc = DateTime.SpecifyKind(rule.UpdatedAtUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture)
    };

    private static bool CanAccessCompany(User callerUser, string company) =>
        callerUser.IsSystemHidden || CompanyScopeHelpers.ResolveCompanies(callerUser).Contains(company, StringComparer.OrdinalIgnoreCase);

    private static async Task<User?> ResolveCallerUserAsync(HttpContext httpContext, IUserRepository users)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext))
        {
            return null;
        }

        if (callerContext.UserId.HasValue)
        {
            var byId = await users.GetByIdAsync(callerContext.UserId.Value);
            if (byId is not null && byId.IsActive)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(callerContext.Email)
            ? null
            : await users.GetByEmailAsync(callerContext.Email);
    }

    private static string? NormalizeRequired(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeCalculationScope(string? value) =>
        string.Equals(value?.Trim(), "company", StringComparison.OrdinalIgnoreCase) ? "company" : "operation";
}
