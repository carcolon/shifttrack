using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Application;

public static class CoverageRuleDefaults
{
    public static readonly DayOfWeek[] OrderedDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    public static CoverageRule[] BuildDefaults(string companyName = "", string? operationName = null) =>
    [
        Build(companyName, operationName, DayOfWeek.Monday, 95, 91, 86),
        Build(companyName, operationName, DayOfWeek.Tuesday, 85, 81, 71),
        Build(companyName, operationName, DayOfWeek.Wednesday, 80, 76, 71),
        Build(companyName, operationName, DayOfWeek.Thursday, 80, 76, 71),
        Build(companyName, operationName, DayOfWeek.Friday, 75, 71, 66),
        Build(companyName, operationName, DayOfWeek.Saturday, 40, 36, 31),
        Build(companyName, operationName, DayOfWeek.Sunday, 35, 31, 26)
    ];

    public static CoverageRule Build(string companyName, string? operationName, DayOfWeek day, int expectedCoverage, int greenThreshold, int yellowThreshold) => new()
    {
        CompanyName = companyName,
        OperationName = string.IsNullOrWhiteSpace(operationName) ? null : operationName.Trim(),
        DayOfWeek = day,
        ExpectedCoverage = expectedCoverage,
        GreenThreshold = greenThreshold,
        YellowThreshold = yellowThreshold,
        CalculationScope = "operation",
        IsActive = true
    };

    public static IReadOnlyDictionary<DayOfWeek, CoverageRule> ToRuleMap(IEnumerable<CoverageRule>? rules)
    {
        var map = BuildDefaults().ToDictionary(rule => rule.DayOfWeek);
        if (rules is null) return map;

        foreach (var rule in rules.Where(rule => rule.IsActive))
        {
            map[rule.DayOfWeek] = rule;
        }

        return map;
    }
}
