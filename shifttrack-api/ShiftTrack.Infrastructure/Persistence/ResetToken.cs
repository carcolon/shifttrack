namespace ShiftTrack.Infrastructure.Persistence;

public sealed class ResetToken
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UsedAtUtc { get; init; }
}
