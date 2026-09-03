namespace ShiftTrack.Domain.Entities;

public class CoverageRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CompanyName { get; init; } = string.Empty;
    public string? OperationName { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public int ExpectedCoverage { get; init; }
    public int GreenThreshold { get; init; }
    public int YellowThreshold { get; init; }
    public string CalculationScope { get; init; } = "operation";
    public bool IsActive { get; init; } = true;
    public string UpdatedBy { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
