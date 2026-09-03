using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Tests.Shared.Builders;

public sealed class PtoRequestBuilder
{
    private Guid _id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private string _userEmail = "charlie.colon@solvoglobal.com";
    private string _userDisplayName = "Charlie Colon";
    private string _requestType = "Vacation";
    private int _numberOfDays = 3;
    private DateTime _startDate = new(2026, 3, 23);
    private DateTime _endDate = new(2026, 3, 25);
    private string? _comments = "Family trip";
    private Guid? _overrideGroupId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private string _status = "pending";
    private string _requestedByEmail = "charlie.colon@solvoglobal.com";
    private string _requestedByName = "Charlie Colon";
    private int _requestedByRole;
    private string? _reviewedByEmail;
    private string? _reviewedByName;
    private int? _reviewedByRole;
    private DateTime? _reviewedAtUtc;
    private DateTime _createdAtUtc = new(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);
    private DateTime _updatedAtUtc = new(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);

    public PtoRequestBuilder WithId(Guid id) { _id = id; return this; }
    public PtoRequestBuilder WithUserId(Guid userId) { _userId = userId; return this; }
    public PtoRequestBuilder WithUserEmail(string email) { _userEmail = email; return this; }
    public PtoRequestBuilder WithUserDisplayName(string displayName) { _userDisplayName = displayName; return this; }
    public PtoRequestBuilder WithRequestType(string requestType) { _requestType = requestType; return this; }
    public PtoRequestBuilder WithNumberOfDays(int days) { _numberOfDays = days; return this; }
    public PtoRequestBuilder StartingOn(DateTime startDate)
    {
        _startDate = startDate;
        _endDate = startDate.AddDays(Math.Max(_numberOfDays - 1, 0));
        return this;
    }
    public PtoRequestBuilder EndingOn(DateTime endDate) { _endDate = endDate; return this; }
    public PtoRequestBuilder WithComments(string? comments) { _comments = comments; return this; }
    public PtoRequestBuilder WithOverrideGroupId(Guid? overrideGroupId) { _overrideGroupId = overrideGroupId; return this; }
    public PtoRequestBuilder Pending() { _status = "pending"; return this; }
    public PtoRequestBuilder Approved(string reviewedByName = "Admin User", string reviewedByEmail = "admin@solvoglobal.com", int reviewedByRole = 2)
    {
        _status = "approved";
        _reviewedByName = reviewedByName;
        _reviewedByEmail = reviewedByEmail;
        _reviewedByRole = reviewedByRole;
        _reviewedAtUtc = new DateTime(2026, 3, 18, 13, 0, 0, DateTimeKind.Utc);
        return this;
    }
    public PtoRequestBuilder Denied(string reviewedByName = "Admin User", string reviewedByEmail = "admin@solvoglobal.com", int reviewedByRole = 2)
    {
        _status = "denied";
        _reviewedByName = reviewedByName;
        _reviewedByEmail = reviewedByEmail;
        _reviewedByRole = reviewedByRole;
        _reviewedAtUtc = new DateTime(2026, 3, 18, 13, 0, 0, DateTimeKind.Utc);
        return this;
    }
    public PtoRequestBuilder RequestedBy(string email, string name, int role)
    {
        _requestedByEmail = email;
        _requestedByName = name;
        _requestedByRole = role;
        return this;
    }

    public PtoRequest Build() => new()
    {
        Id = _id,
        UserId = _userId,
        UserEmail = _userEmail,
        UserDisplayName = _userDisplayName,
        RequestType = _requestType,
        NumberOfDays = _numberOfDays,
        StartDate = _startDate,
        EndDate = _endDate,
        Comments = _comments,
        OverrideGroupId = _overrideGroupId,
        Status = _status,
        RequestedByEmail = _requestedByEmail,
        RequestedByName = _requestedByName,
        RequestedByRole = _requestedByRole,
        ReviewedByEmail = _reviewedByEmail,
        ReviewedByName = _reviewedByName,
        ReviewedByRole = _reviewedByRole,
        ReviewedAtUtc = _reviewedAtUtc,
        CreatedAtUtc = _createdAtUtc,
        UpdatedAtUtc = _updatedAtUtc
    };
}
