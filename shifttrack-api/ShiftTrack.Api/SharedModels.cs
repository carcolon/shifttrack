namespace ShiftTrack.Api;

internal readonly record struct CallerContext
{
    internal int Role { get; init; }
    internal Guid? UserId { get; init; }
    internal string Email { get; init; }
    internal string Name { get; init; }
}
