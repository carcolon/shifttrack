using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api.IntegrationTests.Support;

internal sealed class InMemoryCoverageRuleRepository : ICoverageRuleRepository
{
    private readonly List<CoverageRule> _rules = new();

    public Task<IEnumerable<CoverageRule>> GetRulesAsync(string companyName, string? operationName, bool includeInactive = false)
    {
        var normalizedCompany = Normalize(companyName);
        var normalizedOperation = Normalize(operationName);
        var rules = _rules
            .Where(rule => string.Equals(rule.CompanyName, normalizedCompany, StringComparison.OrdinalIgnoreCase))
            .Where(rule => includeInactive || rule.IsActive)
            .Where(rule => normalizedOperation is null
                ? string.IsNullOrWhiteSpace(rule.OperationName)
                : string.IsNullOrWhiteSpace(rule.OperationName) || string.Equals(rule.OperationName, normalizedOperation, StringComparison.OrdinalIgnoreCase))
            .Select(Clone)
            .ToArray();
        return Task.FromResult<IEnumerable<CoverageRule>>(rules);
    }

    public async Task<CoverageRule[]> ResolveRulesAsync(string companyName, string? operationName)
    {
        var configured = (await GetRulesAsync(companyName, operationName)).ToArray();
        var companyDefault = configured.Where(rule => string.IsNullOrWhiteSpace(rule.OperationName)).ToDictionary(rule => rule.DayOfWeek);
        var operationSpecific = configured.Where(rule => !string.IsNullOrWhiteSpace(rule.OperationName)).ToDictionary(rule => rule.DayOfWeek);

        return CoverageRuleDefaults.OrderedDays.Select(day =>
        {
            if (operationSpecific.TryGetValue(day, out var operationRule)) return operationRule;
            if (companyDefault.TryGetValue(day, out var companyRule)) return companyRule;
            return CoverageRuleDefaults.BuildDefaults(companyName, operationName).First(rule => rule.DayOfWeek == day);
        }).ToArray();
    }

    public Task<int> UpsertRulesAsync(IEnumerable<CoverageRule> rules)
    {
        var count = 0;
        foreach (var rule in rules)
        {
            var normalizedRule = Clone(rule);
            var index = _rules.FindIndex(item =>
                string.Equals(item.CompanyName, normalizedRule.CompanyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.OperationName ?? string.Empty, normalizedRule.OperationName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                item.DayOfWeek == normalizedRule.DayOfWeek);
            if (index >= 0) _rules[index] = normalizedRule;
            else _rules.Add(normalizedRule);
            count++;
        }

        return Task.FromResult(count);
    }

    private static CoverageRule Clone(CoverageRule rule) => new()
    {
        Id = rule.Id,
        CompanyName = Normalize(rule.CompanyName),
        OperationName = Normalize(rule.OperationName),
        DayOfWeek = rule.DayOfWeek,
        ExpectedCoverage = rule.ExpectedCoverage,
        GreenThreshold = rule.GreenThreshold,
        YellowThreshold = rule.YellowThreshold,
        CalculationScope = string.Equals(rule.CalculationScope, "company", StringComparison.OrdinalIgnoreCase) ? "company" : "operation",
        IsActive = rule.IsActive,
        UpdatedBy = rule.UpdatedBy,
        UpdatedAtUtc = rule.UpdatedAtUtc
    };

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
