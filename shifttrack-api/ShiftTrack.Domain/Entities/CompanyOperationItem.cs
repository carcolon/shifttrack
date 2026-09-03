namespace ShiftTrack.Domain.Entities;

public class CompanyOperationItem
{
    public string CompanyName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
