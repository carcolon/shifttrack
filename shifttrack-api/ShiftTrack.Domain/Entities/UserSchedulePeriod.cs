namespace ShiftTrack.Domain.Entities;

public class UserSchedulePeriod
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string ShiftTime { get; init; } = string.Empty;
    public string BlocksJson { get; init; } = "[]";
    public bool IsRepeating { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
