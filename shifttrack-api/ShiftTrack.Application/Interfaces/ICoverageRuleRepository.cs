using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Application.Interfaces;

public interface ICoverageRuleRepository
{
    Task<IEnumerable<CoverageRule>> GetRulesAsync(string companyName, string? operationName, bool includeInactive = false);
    Task<CoverageRule[]> ResolveRulesAsync(string companyName, string? operationName);
    Task<int> UpsertRulesAsync(IEnumerable<CoverageRule> rules);
}
