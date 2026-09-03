namespace ShiftTrack.Domain.Entities;

public class SwapRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RequestedByUserId { get; init; }
    public string RequestedByEmail { get; init; } = string.Empty;
    public string RequestedByDisplayName { get; init; } = string.Empty;
    public int RequestedByRole { get; init; }
    public Guid TargetUserId { get; init; }
    public string TargetUserEmail { get; init; } = string.Empty;
    public string TargetUserDisplayName { get; init; } = string.Empty;
    public int TargetUserRole { get; init; }
    public DateTime SwapDate { get; init; }
    public string RequestedDatesJson { get; init; } = "[]";
    public string TargetDatesJson { get; init; } = "[]";
    public string PairingsJson { get; init; } = "[]";
    public string RequestType { get; init; } = "swap_shift";
    public string? Comments { get; init; }
    public string? ReviewComments { get; init; }
    public string WeeklyHoursJson { get; init; } = "[]";
    public string Status { get; init; } = "pending";
    public Guid? AppliedGroupId { get; init; }
    public string? ReviewedByEmail { get; init; }
    public string? ReviewedByName { get; init; }
    public int? ReviewedByRole { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
