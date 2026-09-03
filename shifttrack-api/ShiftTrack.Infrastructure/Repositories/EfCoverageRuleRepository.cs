using Microsoft.EntityFrameworkCore;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;
using ShiftTrack.Infrastructure.Persistence;

namespace ShiftTrack.Infrastructure.Repositories;

public sealed class EfCoverageRuleRepository(ShiftTrackDbContext dbContext) : ICoverageRuleRepository
{
    public async Task<IEnumerable<CoverageRule>> GetRulesAsync(string companyName, string? operationName, bool includeInactive = false)
    {
        var normalizedCompany = NormalizeCompany(companyName);
        var normalizedOperation = NormalizeOperation(operationName);

        var rules = await dbContext.CoverageRules
            .AsNoTracking()
            .Where(rule => rule.CompanyName == normalizedCompany)
            .Where(rule => includeInactive || rule.IsActive)
            .Where(rule => normalizedOperation == null
                ? rule.OperationName == null || rule.OperationName == string.Empty
                : rule.OperationName == normalizedOperation || rule.OperationName == null || rule.OperationName == string.Empty)
            .ToArrayAsync();

        return rules
            .Select(NormalizeRule)
            .OrderBy(rule => string.Equals(rule.OperationName, normalizedOperation, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(rule => rule.DayOfWeek)
            .ToArray();
    }

    public async Task<CoverageRule[]> ResolveRulesAsync(string companyName, string? operationName)
    {
        var configured = (await GetRulesAsync(companyName, operationName)).ToArray();
        var companyDefault = configured
            .Where(rule => string.IsNullOrWhiteSpace(rule.OperationName))
            .ToDictionary(rule => rule.DayOfWeek);
        var operationSpecific = configured
            .Where(rule => !string.IsNullOrWhiteSpace(rule.OperationName) &&
                           string.Equals(rule.OperationName, operationName?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToDictionary(rule => rule.DayOfWeek);

        return CoverageRuleDefaults.OrderedDays
            .Select(day =>
            {
                if (operationSpecific.TryGetValue(day, out var operationRule)) return operationRule;
                if (companyDefault.TryGetValue(day, out var companyRule)) return companyRule;
                return CoverageRuleDefaults.Build(NormalizeCompany(companyName), NormalizeOperation(operationName), day,
                    CoverageRuleDefaults.ToRuleMap(null)[day].ExpectedCoverage,
                    CoverageRuleDefaults.ToRuleMap(null)[day].GreenThreshold,
                    CoverageRuleDefaults.ToRuleMap(null)[day].YellowThreshold);
            })
            .ToArray();
    }

    public async Task<int> UpsertRulesAsync(IEnumerable<CoverageRule> rules)
    {
        var affected = 0;

        foreach (var rule in rules)
        {
            var companyName = NormalizeCompany(rule.CompanyName);
            var operationName = NormalizeOperation(rule.OperationName) ?? string.Empty;
            var calculationScope = NormalizeCalculationScope(rule.CalculationScope);
            var dayOfWeek = rule.DayOfWeek;

            var updated = await dbContext.CoverageRules
                .Where(existing => existing.CompanyName == companyName &&
                                   existing.OperationName == operationName &&
                                   existing.DayOfWeek == dayOfWeek)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(existing => existing.ExpectedCoverage, rule.ExpectedCoverage)
                    .SetProperty(existing => existing.GreenThreshold, rule.GreenThreshold)
                    .SetProperty(existing => existing.YellowThreshold, rule.YellowThreshold)
                    .SetProperty(existing => existing.CalculationScope, calculationScope)
                    .SetProperty(existing => existing.IsActive, rule.IsActive)
                    .SetProperty(existing => existing.UpdatedBy, rule.UpdatedBy)
                    .SetProperty(existing => existing.UpdatedAtUtc, rule.UpdatedAtUtc));

            if (updated > 0)
            {
                affected += updated;
                continue;
            }

            dbContext.CoverageRules.Add(new CoverageRule
            {
                Id = rule.Id,
                CompanyName = companyName,
                OperationName = operationName,
                DayOfWeek = dayOfWeek,
                ExpectedCoverage = rule.ExpectedCoverage,
                GreenThreshold = rule.GreenThreshold,
                YellowThreshold = rule.YellowThreshold,
                CalculationScope = calculationScope,
                IsActive = rule.IsActive,
                UpdatedBy = rule.UpdatedBy,
                UpdatedAtUtc = rule.UpdatedAtUtc
            });
            affected += await dbContext.SaveChangesAsync();
        }

        return affected;
    }

    private static CoverageRule NormalizeRule(CoverageRule rule) => new()
    {
        Id = rule.Id,
        CompanyName = rule.CompanyName,
        OperationName = NormalizeOperation(rule.OperationName),
        DayOfWeek = rule.DayOfWeek,
        ExpectedCoverage = rule.ExpectedCoverage,
        GreenThreshold = rule.GreenThreshold,
        YellowThreshold = rule.YellowThreshold,
        CalculationScope = NormalizeCalculationScope(rule.CalculationScope),
        IsActive = rule.IsActive,
        UpdatedBy = rule.UpdatedBy,
        UpdatedAtUtc = rule.UpdatedAtUtc
    };

    private static string NormalizeCompany(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOperation(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeCalculationScope(string? value) =>
        string.Equals(value?.Trim(), "company", StringComparison.OrdinalIgnoreCase) ? "company" : "operation";
}
