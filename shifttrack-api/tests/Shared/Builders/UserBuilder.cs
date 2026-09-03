using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Tests.Shared.Builders;

public sealed class UserBuilder
{
    private Guid _id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private Guid _tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private Guid _objectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private string _email = "test.user@solvoglobal.com";
    private string? _displayName = "Test User";
    private int _role;
    private bool _isActive = true;
    private bool _isSystemHidden;
    private string? _passwordHash = "hashed-password";
    private DateTime _createdAtUtc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private bool _mustChangePassword;
    private string _location = "COL";
    private string _company = "Solvo Global";
    private string? _companyScope;
    private string _operation = "Leaders";
    private string _shiftTime = "Morning";
    private string? _scheduleBlocks = """[{"days":["Mon","Tue","Wed","Thu","Fri"],"start":"09:36","end":"21:36"}]""";
    private IReadOnlyList<UserSchedulePeriod> _schedulePeriods = Array.Empty<UserSchedulePeriod>();

    public UserBuilder WithId(Guid id) { _id = id; return this; }
    public UserBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
    public UserBuilder WithObjectId(Guid objectId) { _objectId = objectId; return this; }
    public UserBuilder WithEmail(string email) { _email = email; return this; }
    public UserBuilder WithDisplayName(string? displayName) { _displayName = displayName; return this; }
    public UserBuilder WithRole(int role) { _role = role; return this; }
    public UserBuilder AsEmployee() => WithRole(0);
    public UserBuilder AsManager() => WithRole(1);
    public UserBuilder AsAdmin() => WithRole(2);
    public UserBuilder Active() { _isActive = true; return this; }
    public UserBuilder Inactive() { _isActive = false; return this; }
    public UserBuilder SystemHidden(bool isSystemHidden = true) { _isSystemHidden = isSystemHidden; return this; }
    public UserBuilder MustChangePassword(bool mustChangePassword = true) { _mustChangePassword = mustChangePassword; return this; }
    public UserBuilder WithPasswordHash(string? passwordHash) { _passwordHash = passwordHash; return this; }
    public UserBuilder WithCreatedAtUtc(DateTime createdAtUtc) { _createdAtUtc = createdAtUtc; return this; }
    public UserBuilder WithLocation(string location) { _location = location; return this; }
    public UserBuilder WithCompany(string company) { _company = company; return this; }
    public UserBuilder WithCompanyScope(string? companyScope) { _companyScope = companyScope; return this; }
    public UserBuilder WithOperation(string operation) { _operation = operation; return this; }
    public UserBuilder WithShiftTime(string shiftTime) { _shiftTime = shiftTime; return this; }
    public UserBuilder WithScheduleBlocks(string? scheduleBlocks) { _scheduleBlocks = scheduleBlocks; return this; }
    public UserBuilder WithSchedulePeriods(params UserSchedulePeriod[] periods)
    {
        _schedulePeriods = periods;
        return this;
    }

    public User Build() => new()
    {
        Id = _id,
        TenantId = _tenantId,
        ObjectId = _objectId,
        Email = _email,
        DisplayName = _displayName,
        Role = _role,
        IsActive = _isActive,
        IsSystemHidden = _isSystemHidden,
        PasswordHash = _passwordHash,
        CreatedAtUtc = _createdAtUtc,
        MustChangePassword = _mustChangePassword,
        Location = _location,
        Company = _company,
        CompanyScope = _companyScope,
        Operation = _operation,
        ShiftTime = _shiftTime,
        ScheduleBlocks = _scheduleBlocks,
        SchedulePeriods = _schedulePeriods
    };
}
