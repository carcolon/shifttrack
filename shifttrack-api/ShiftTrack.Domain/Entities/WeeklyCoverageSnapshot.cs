namespace ShiftTrack.Domain.Entities;

public class WeeklyCoverageSnapshot
{
    public DateTime WeekStartDate { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? ItemsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
