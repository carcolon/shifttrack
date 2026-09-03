namespace ShiftTrack.Domain.Entities;

public class UserScheduleOverride
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public DateTime OverrideDate { get; init; }
    public Guid? GroupId { get; init; }
    public string EntryType { get; init; } = string.Empty;
    public string? RequestType { get; init; }
    public string? Comments { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
    public string? Label { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
