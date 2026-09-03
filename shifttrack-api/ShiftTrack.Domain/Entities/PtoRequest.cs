namespace ShiftTrack.Domain.Entities;

public class PtoRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string UserEmail { get; init; } = string.Empty;
    public string UserDisplayName { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public int NumberOfDays { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string? Comments { get; init; }
    public string? ReviewComments { get; init; }
    public Guid? OverrideGroupId { get; init; }
    public string Status { get; init; } = "pending";
    public string RequestedByEmail { get; init; } = string.Empty;
    public string RequestedByName { get; init; } = string.Empty;
    public int RequestedByRole { get; init; }
    public string? ReviewedByEmail { get; init; }
    public string? ReviewedByName { get; init; }
    public int? ReviewedByRole { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
