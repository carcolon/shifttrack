using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Tests.Shared.Builders;

public sealed class UserSchedulePeriodBuilder
{
    private Guid _id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private DateTime _effectiveFrom = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime? _effectiveTo;
    private string _shiftTime = "Morning";
    private string _blocksJson = """[{"days":["Mon","Tue","Wed","Thu","Fri"],"start":"09:36","end":"21:36"}]""";
    private bool _isRepeating;
    private DateTime _createdAtUtc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    public UserSchedulePeriodBuilder WithId(Guid id) { _id = id; return this; }
    public UserSchedulePeriodBuilder WithUserId(Guid userId) { _userId = userId; return this; }
    public UserSchedulePeriodBuilder EffectiveFrom(DateTime effectiveFrom) { _effectiveFrom = effectiveFrom; return this; }
    public UserSchedulePeriodBuilder EffectiveTo(DateTime? effectiveTo) { _effectiveTo = effectiveTo; return this; }
    public UserSchedulePeriodBuilder WithShiftTime(string shiftTime) { _shiftTime = shiftTime; return this; }
    public UserSchedulePeriodBuilder WithBlocksJson(string blocksJson) { _blocksJson = blocksJson; return this; }
    public UserSchedulePeriodBuilder Repeating(bool isRepeating = true) { _isRepeating = isRepeating; return this; }
    public UserSchedulePeriodBuilder WithCreatedAtUtc(DateTime createdAtUtc) { _createdAtUtc = createdAtUtc; return this; }

    public UserSchedulePeriod Build() => new()
    {
        Id = _id,
        UserId = _userId,
        EffectiveFrom = _effectiveFrom,
        EffectiveTo = _effectiveTo,
        ShiftTime = _shiftTime,
        BlocksJson = _blocksJson,
        IsRepeating = _isRepeating,
        CreatedAtUtc = _createdAtUtc
    };
}
