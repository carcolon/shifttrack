using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Tests.Shared.Builders;

public sealed class SwapRequestBuilder
{
    private Guid _id = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private Guid _requestedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private string _requestedByEmail = "carlos.colon@solvoglobal.com";
    private string _requestedByDisplayName = "Carlos Colon";
    private int _requestedByRole;
    private Guid _targetUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private string _targetUserEmail = "sara.puerta@solvoglobal.com";
    private string _targetUserDisplayName = "Sara Puerta";
    private int _targetUserRole;
    private DateTime _swapDate = new(2026, 3, 20);
    private string _requestedDatesJson = """["2026-03-20"]""";
    private string _targetDatesJson = """["2026-04-03"]""";
    private string _pairingsJson =
        """[{"requesterDate":"2026-04-03","targetDate":"2026-03-20","requesterBefore":"Day Off","requesterAfter":"08:00 - 17:00","targetBefore":"08:00 - 17:00","targetAfter":"Day Off"}]""";
    private string _requestType = "swap_shift";
    private string? _comments = "Need to trade days";
    private string _status = "pending";
    private Guid? _appliedGroupId;
    private string? _reviewedByEmail;
    private string? _reviewedByName;
    private int? _reviewedByRole;
    private DateTime? _reviewedAtUtc;
    private DateTime _createdAtUtc = new(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);
    private DateTime _updatedAtUtc = new(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);

    public SwapRequestBuilder WithId(Guid id) { _id = id; return this; }
    public SwapRequestBuilder RequestedBy(Guid userId, string email, string displayName, int role)
    {
        _requestedByUserId = userId;
        _requestedByEmail = email;
        _requestedByDisplayName = displayName;
        _requestedByRole = role;
        return this;
    }
    public SwapRequestBuilder Target(Guid userId, string email, string displayName, int role)
    {
        _targetUserId = userId;
        _targetUserEmail = email;
        _targetUserDisplayName = displayName;
        _targetUserRole = role;
        return this;
    }
    public SwapRequestBuilder WithSwapDate(DateTime swapDate) { _swapDate = swapDate; return this; }
    public SwapRequestBuilder WithRequestedDatesJson(string requestedDatesJson) { _requestedDatesJson = requestedDatesJson; return this; }
    public SwapRequestBuilder WithTargetDatesJson(string targetDatesJson) { _targetDatesJson = targetDatesJson; return this; }
    public SwapRequestBuilder WithPairingsJson(string pairingsJson) { _pairingsJson = pairingsJson; return this; }
    public SwapRequestBuilder WithRequestType(string requestType) { _requestType = requestType; return this; }
    public SwapRequestBuilder WithComments(string? comments) { _comments = comments; return this; }
    public SwapRequestBuilder Pending() { _status = "pending"; return this; }
    public SwapRequestBuilder Approved(string reviewedByName = "Sara Puerta", string reviewedByEmail = "sara.puerta@solvoglobal.com", int reviewedByRole = 0)
    {
        _status = "approved";
        _reviewedByName = reviewedByName;
        _reviewedByEmail = reviewedByEmail;
        _reviewedByRole = reviewedByRole;
        _reviewedAtUtc = new DateTime(2026, 3, 18, 14, 0, 0, DateTimeKind.Utc);
        _appliedGroupId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        return this;
    }
    public SwapRequestBuilder Denied(string reviewedByName = "Sara Puerta", string reviewedByEmail = "sara.puerta@solvoglobal.com", int reviewedByRole = 0)
    {
        _status = "denied";
        _reviewedByName = reviewedByName;
        _reviewedByEmail = reviewedByEmail;
        _reviewedByRole = reviewedByRole;
        _reviewedAtUtc = new DateTime(2026, 3, 18, 14, 0, 0, DateTimeKind.Utc);
        _appliedGroupId = null;
        return this;
    }

    public SwapRequest Build() => new()
    {
        Id = _id,
        RequestedByUserId = _requestedByUserId,
        RequestedByEmail = _requestedByEmail,
        RequestedByDisplayName = _requestedByDisplayName,
        RequestedByRole = _requestedByRole,
        TargetUserId = _targetUserId,
        TargetUserEmail = _targetUserEmail,
        TargetUserDisplayName = _targetUserDisplayName,
        TargetUserRole = _targetUserRole,
        SwapDate = _swapDate,
        RequestedDatesJson = _requestedDatesJson,
        TargetDatesJson = _targetDatesJson,
        PairingsJson = _pairingsJson,
        RequestType = _requestType,
        Comments = _comments,
        Status = _status,
        AppliedGroupId = _appliedGroupId,
        ReviewedByEmail = _reviewedByEmail,
        ReviewedByName = _reviewedByName,
        ReviewedByRole = _reviewedByRole,
        ReviewedAtUtc = _reviewedAtUtc,
        CreatedAtUtc = _createdAtUtc,
        UpdatedAtUtc = _updatedAtUtc
    };
}
