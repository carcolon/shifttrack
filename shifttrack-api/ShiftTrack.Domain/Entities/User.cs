namespace ShiftTrack.Domain.Entities;

public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid ObjectId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public int Role { get; init; }
    public bool IsActive { get; init; }
    public bool IsSystemHidden { get; init; }
    public string? PasswordHash { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public bool MustChangePassword { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string? CompanyScope { get; init; }
    public string Operation { get; init; } = string.Empty;
    public string ShiftTime { get; init; } = string.Empty;
    public string? ScheduleBlocks { get; init; }
    public IReadOnlyList<UserSchedulePeriod> SchedulePeriods { get; set; } = Array.Empty<UserSchedulePeriod>();
}
