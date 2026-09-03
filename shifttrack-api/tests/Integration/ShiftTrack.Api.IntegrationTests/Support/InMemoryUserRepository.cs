using System.Text.Json;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api.IntegrationTests.Support;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users;
    private readonly List<ScheduleEvent> _events = new();
    private readonly List<UserScheduleOverride> _overrides = new();
    private readonly List<PtoRequest> _ptoRequests = new();
    private readonly List<SwapRequest> _swapRequests = new();
    private readonly List<CompanyCatalogItem> _companies = new();
    private readonly List<CompanyOperationItem> _companyOperations = new();

    public InMemoryUserRepository(IEnumerable<User>? seedUsers = null)
    {
        _users = seedUsers?.Select(user => CloneUser(user)).ToList() ?? BuildDefaultUsers();
        _companies = _users
            .Select(user => user.Company)
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(company => new CompanyCatalogItem { Name = company, IsActive = true, CreatedAtUtc = DateTime.UtcNow })
            .ToList();
        _companyOperations = _users
            .Where(user => !string.IsNullOrWhiteSpace(user.Company) && !string.IsNullOrWhiteSpace(user.Operation))
            .Select(user => new { user.Company, user.Operation })
            .DistinctBy(item => $"{item.Company.Trim().ToLowerInvariant()}::{item.Operation.Trim().ToLowerInvariant()}")
            .Select(item => new CompanyOperationItem { CompanyName = item.Company.Trim(), Name = item.Operation.Trim(), IsActive = true, CreatedAtUtc = DateTime.UtcNow })
            .ToList();
        foreach (var operation in new[] { "ESQ", "Leaders", "Outbound", "Referral", "SGF" })
        {
            if (!_companyOperations.Any(item => string.Equals(item.CompanyName, "Esquire Law, LLC", StringComparison.OrdinalIgnoreCase) &&
                                               string.Equals(item.Name, operation, StringComparison.OrdinalIgnoreCase)))
            {
                _companyOperations.Add(new CompanyOperationItem { CompanyName = "Esquire Law, LLC", Name = operation, IsActive = true, CreatedAtUtc = DateTime.UtcNow });
            }
        }
    }

    public Task<User?> GetByEmailAsync(string email) =>
        Task.FromResult(_users.FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) is User user ? CloneUser(user) : null);

    public Task<User?> GetByObjectIdAsync(Guid objectId) =>
        Task.FromResult(_users.FirstOrDefault(user => user.ObjectId == objectId) is User user ? CloneUser(user) : null);

    public Task<User?> GetByIdAsync(Guid id) =>
        Task.FromResult(_users.FirstOrDefault(user => user.Id == id) is User user ? CloneUser(user) : null);

    public Task<IEnumerable<User>> GetAllAsync() =>
        Task.FromResult<IEnumerable<User>>(_users.Where(user => user.IsActive && !user.IsSystemHidden).Select(user => CloneUser(user)).ToArray());

    public Task<IEnumerable<User>> GetInactiveAsync() =>
        Task.FromResult<IEnumerable<User>>(_users.Where(user => !user.IsActive && !user.IsSystemHidden).Select(user => CloneUser(user)).ToArray());

    public Task<IEnumerable<User>> GetSystemHiddenAsync() =>
        Task.FromResult<IEnumerable<User>>(_users.Where(user => user.IsActive && user.IsSystemHidden).Select(user => CloneUser(user)).ToArray());

    public Task<int> UpdatePasswordAsync(string email, string passwordHash, bool mustChangePassword = false)
    {
        var user = _users.FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        if (user is null) return Task.FromResult(0);
        ReplaceUser(CloneUser(user, passwordHash: passwordHash, mustChangePassword: mustChangePassword));
        return Task.FromResult(1);
    }

    public Task<bool> EmailExistsAsync(string email) =>
        Task.FromResult(_users.Any(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<int> SetSystemHiddenAsync(Guid id, bool isSystemHidden)
    {
        var user = _users.FirstOrDefault(item => item.Id == id);
        if (user is null) return Task.FromResult(0);
        ReplaceUser(CloneUser(user, isSystemHidden: isSystemHidden));
        return Task.FromResult(1);
    }

    public Task<int> CountActiveSystemHiddenAsync() =>
        Task.FromResult(_users.Count(user => user.IsActive && user.IsSystemHidden));

    public Task<int> CreateUserAsync(User user)
    {
        _users.Add(CloneUser(user));
        return Task.FromResult(1);
    }

    public Task<int> UpdateObjectIdAsync(Guid id, Guid objectId)
    {
        var user = _users.FirstOrDefault(item => item.Id == id);
        if (user is null) return Task.FromResult(0);
        ReplaceUser(CloneUser(user, objectId: objectId));
        return Task.FromResult(1);
    }

    public Task<int> UpdateUserAsync(Guid id, string displayName, int role, string location, string company, string? companyScope, string operation, string shiftTime, string? scheduleBlocks)
    {
        var user = _users.FirstOrDefault(item => item.Id == id);
        if (user is null) return Task.FromResult(0);
        ReplaceUser(CloneUser(user,
            displayName: displayName,
            role: role,
            location: location,
            company: company,
            companyScope: companyScope,
            operation: operation,
            shiftTime: shiftTime,
            scheduleBlocks: scheduleBlocks));
        return Task.FromResult(1);
    }

    public Task<int> BulkUpsertUsersAsync(IEnumerable<BulkUserUpsert> users)
    {
        var affected = 0;
        foreach (var item in users)
        {
            if (item.IsNewUser)
            {
                _users.Add(CloneUser(item.User, schedulePeriods: item.SchedulePeriods.Select(ClonePeriod).ToArray()));
                affected++;
                continue;
            }

            var existing = _users.FirstOrDefault(user => user.Id == item.User.Id);
            if (existing is null) continue;

            ReplaceUser(CloneUser(existing,
                displayName: item.User.DisplayName,
                role: item.User.Role,
                location: item.User.Location,
                company: item.User.Company,
                companyScope: item.User.CompanyScope,
                operation: item.User.Operation,
                shiftTime: item.LegacyShiftTime,
                scheduleBlocks: item.LegacyScheduleBlocks,
                schedulePeriods: item.SchedulePeriods.Select(ClonePeriod).ToArray()));
            affected++;
        }

        return Task.FromResult(affected);
    }

    public Task<IEnumerable<CompanyCatalogItem>> GetCompaniesAsync(bool includeInactive) =>
        Task.FromResult<IEnumerable<CompanyCatalogItem>>(_companies
            .Where(company => includeInactive || company.IsActive)
            .OrderBy(company => company.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CloneCompany)
            .ToArray());

    public Task<int> UpsertCompanyAsync(string name, bool isActive)
    {
        var index = _companies.FindIndex(company => string.Equals(company.Name, name, StringComparison.OrdinalIgnoreCase));
        var item = new CompanyCatalogItem
        {
            Name = name,
            IsActive = isActive,
            CreatedAtUtc = index >= 0 ? _companies[index].CreatedAtUtc : DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        if (index >= 0) _companies[index] = item;
        else _companies.Add(item);
        return Task.FromResult(1);
    }

    public Task<int> SetCompanyActiveAsync(string name, bool isActive) => UpsertCompanyAsync(name, isActive);

    public Task<int> RenameCompanyAsync(string currentName, string newName)
    {
        var company = _companies.FirstOrDefault(item => string.Equals(item.Name, currentName, StringComparison.OrdinalIgnoreCase));
        if (company is not null)
        {
            _companies.Remove(company);
            _companies.Add(new CompanyCatalogItem { Name = newName, IsActive = true, CreatedAtUtc = company.CreatedAtUtc, UpdatedAtUtc = DateTime.UtcNow });
        }

        for (var i = 0; i < _users.Count; i++)
        {
            var user = _users[i];
            if (!string.Equals(user.Company, currentName, StringComparison.OrdinalIgnoreCase)) continue;
            _users[i] = CloneUser(user, company: newName, companyScope: CompanyScopeHelpers.BuildCompanyScopeJson(CompanyScopeHelpers.ResolveCompanies(user).Select(item => string.Equals(item, currentName, StringComparison.OrdinalIgnoreCase) ? newName : item), newName));
        }

        for (var i = 0; i < _companyOperations.Count; i++)
        {
            var operation = _companyOperations[i];
            if (!string.Equals(operation.CompanyName, currentName, StringComparison.OrdinalIgnoreCase)) continue;
            _companyOperations[i] = new CompanyOperationItem { CompanyName = newName, Name = operation.Name, IsActive = operation.IsActive, CreatedAtUtc = operation.CreatedAtUtc, UpdatedAtUtc = DateTime.UtcNow };
        }

        return Task.FromResult(1);
    }

    public Task<IEnumerable<CompanyOperationItem>> GetCompanyOperationsAsync(string? companyName, bool includeInactive) =>
        Task.FromResult<IEnumerable<CompanyOperationItem>>(_companyOperations
            .Where(item => (string.IsNullOrWhiteSpace(companyName) || string.Equals(item.CompanyName, companyName, StringComparison.OrdinalIgnoreCase)) &&
                           (includeInactive || item.IsActive))
            .OrderBy(item => item.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CloneCompanyOperation)
            .ToArray());

    public Task<int> UpsertCompanyOperationAsync(string companyName, string name, bool isActive)
    {
        var index = _companyOperations.FindIndex(item =>
            string.Equals(item.CompanyName, companyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        var item = new CompanyOperationItem
        {
            CompanyName = companyName,
            Name = name,
            IsActive = isActive,
            CreatedAtUtc = index >= 0 ? _companyOperations[index].CreatedAtUtc : DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        if (index >= 0) _companyOperations[index] = item;
        else _companyOperations.Add(item);
        return Task.FromResult(1);
    }

    public Task<int> SetCompanyOperationActiveAsync(string companyName, string name, bool isActive) =>
        UpsertCompanyOperationAsync(companyName, name, isActive);

    public Task<int> RenameCompanyOperationAsync(string companyName, string currentName, string newName)
    {
        var index = _companyOperations.FindIndex(item =>
            string.Equals(item.CompanyName, companyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Name, currentName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            var current = _companyOperations[index];
            _companyOperations[index] = new CompanyOperationItem { CompanyName = companyName, Name = newName, IsActive = true, CreatedAtUtc = current.CreatedAtUtc, UpdatedAtUtc = DateTime.UtcNow };
        }
        foreach (var user in _users.Where(user => string.Equals(user.Company, companyName, StringComparison.OrdinalIgnoreCase) &&
                                                  string.Equals(user.Operation, currentName, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            ReplaceUser(CloneUser(user, operation: newName));
        }
        return Task.FromResult(1);
    }

    public Task<int> ReplaceUserSchedulePeriodsAsync(Guid userId, IEnumerable<UserSchedulePeriod> periods, string shiftTime, string? scheduleBlocks)
    {
        var user = _users.FirstOrDefault(item => item.Id == userId);
        if (user is null) return Task.FromResult(0);
        ReplaceUser(CloneUser(user,
            shiftTime: shiftTime,
            scheduleBlocks: scheduleBlocks,
            schedulePeriods: periods.Select(ClonePeriod).ToArray()));
        return Task.FromResult(1);
    }

    public Task<int> SoftDeleteAsync(Guid id)
    {
        var user = _users.FirstOrDefault(item => item.Id == id);
        if (user is null) return Task.FromResult(0);
        ReplaceUser(CloneUser(user, isActive: false));
        return Task.FromResult(1);
    }

    public Task<int> ReactivateAsync(Guid id)
    {
        var user = _users.FirstOrDefault(item => item.Id == id);
        if (user is null) return Task.FromResult(0);
        ReplaceUser(CloneUser(user, isActive: true));
        return Task.FromResult(1);
    }

    public Task<int> HardDeleteAsync(Guid id)
    {
        var removed = _users.RemoveAll(user => user.Id == id);
        return Task.FromResult(removed > 0 ? 1 : 0);
    }

    public Task<int> CreateScheduleEventAsync(ScheduleEvent scheduleEvent)
    {
        _events.Add(scheduleEvent);
        return Task.FromResult(1);
    }

    public Task<IEnumerable<ScheduleEvent>> GetRecentScheduleEventsAsync(int take) =>
        Task.FromResult<IEnumerable<ScheduleEvent>>(_events.OrderByDescending(item => item.OccurredAtUtc).Take(take).ToArray());

    public Task<IEnumerable<UserScheduleOverride>> GetScheduleOverridesAsync(DateTime fromDate, DateTime toDate) =>
        Task.FromResult<IEnumerable<UserScheduleOverride>>(_overrides
            .Where(item => item.OverrideDate.Date >= fromDate.Date && item.OverrideDate.Date <= toDate.Date)
            .ToArray());

    public Task<int> RemoveScheduleOverridesByGroupAsync(Guid userId, Guid groupId)
    {
        var removed = _overrides.RemoveAll(item => item.UserId == userId && item.GroupId == groupId);
        return Task.FromResult(removed);
    }

    public Task<IEnumerable<UserScheduleOverride>> GetScheduleOverridesByGroupAsync(Guid groupId) =>
        Task.FromResult<IEnumerable<UserScheduleOverride>>(_overrides.Where(item => item.GroupId == groupId).ToArray());

    public Task<Guid> ApplyPtoOverrideAsync(Guid userId, DateTime startDate, int numberOfDays, string requestType, string? comments, Guid? existingGroupId)
    {
        var dates = Enumerable.Range(0, numberOfDays).Select(offset => startDate.Date.AddDays(offset)).ToArray();
        return ApplyPtoOverrideDatesAsync(userId, dates, requestType, comments, existingGroupId);
    }

    public Task<Guid> ApplyPtoOverrideDatesAsync(Guid userId, IEnumerable<DateTime> overrideDates, string requestType, string? comments, Guid? existingGroupId)
    {
        var groupId = existingGroupId ?? Guid.NewGuid();
        foreach (var day in overrideDates.Select(date => date.Date).Distinct().OrderBy(date => date))
        {
            _overrides.Add(new UserScheduleOverride
            {
                UserId = userId,
                OverrideDate = day,
                EntryType = "pto",
                RequestType = requestType,
                Label = "PTO",
                GroupId = groupId,
                Comments = comments
            });
        }

        return Task.FromResult(groupId);
    }

    public Task<int> UpsertScheduleOverrideAsync(UserScheduleOverride scheduleOverride)
    {
        var index = _overrides.FindIndex(item => item.UserId == scheduleOverride.UserId && item.OverrideDate.Date == scheduleOverride.OverrideDate.Date);
        if (index >= 0)
        {
            _overrides[index] = scheduleOverride;
        }
        else
        {
            _overrides.Add(scheduleOverride);
        }

        return Task.FromResult(1);
    }

    public Task<int> UpsertPtoRequestAsync(PtoRequest request)
    {
        var index = _ptoRequests.FindIndex(item => item.Id == request.Id);
        if (index >= 0)
        {
            _ptoRequests[index] = ClonePtoRequest(request);
        }
        else
        {
            _ptoRequests.Add(ClonePtoRequest(request));
        }

        return Task.FromResult(1);
    }

    public Task<PtoRequest?> GetPtoRequestAsync(Guid requestId) =>
        Task.FromResult(_ptoRequests.FirstOrDefault(item => item.Id == requestId) is PtoRequest request ? ClonePtoRequest(request) : null);

    public Task<PtoRequest?> GetLatestPtoRequestByGroupIdAsync(Guid groupId)
    {
        var request = _ptoRequests
            .Where(item => item.Id == groupId || item.OverrideGroupId == groupId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        return Task.FromResult(request is null ? null : ClonePtoRequest(request));
    }

    public Task<PtoRequest?> GetOverlappingActivePtoRequestAsync(Guid userId, DateTime startDate, DateTime endDate, Guid? excludingRequestOrGroupId)
    {
        var request = _ptoRequests
            .Where(item => item.UserId == userId)
            .Where(item => string.Equals(item.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(item.Status, "approved", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.StartDate.Date <= endDate.Date && item.EndDate.Date >= startDate.Date)
            .Where(item => !excludingRequestOrGroupId.HasValue ||
                           (item.Id != excludingRequestOrGroupId.Value && item.OverrideGroupId != excludingRequestOrGroupId.Value))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        return Task.FromResult(request is null ? null : ClonePtoRequest(request));
    }

    public Task<IEnumerable<PtoRequest>> GetPtoRequestsAsync(string? status, int take)
    {
        var items = _ptoRequests
            .Where(item => string.IsNullOrWhiteSpace(status) || string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .Select(ClonePtoRequest)
            .ToArray();
        return Task.FromResult<IEnumerable<PtoRequest>>(items);
    }
    public Task<int> UpdatePtoRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? overrideGroupId) => Task.FromResult(1);
    public Task<int> UpdatePtoRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? overrideGroupId, string? reviewComments) =>
        UpdatePtoRequestStatusAsync(requestId, status, reviewedByEmail, reviewedByName, reviewedByRole, overrideGroupId);
    public Task<int> CreateSwapRequestAsync(SwapRequest request)
    {
        _swapRequests.Add(CloneSwapRequest(request));
        return Task.FromResult(1);
    }

    public Task<SwapRequest?> GetSwapRequestAsync(Guid requestId) =>
        Task.FromResult(_swapRequests.FirstOrDefault(item => item.Id == requestId) is SwapRequest request ? CloneSwapRequest(request) : null);

    public Task<IEnumerable<SwapRequest>> GetSwapRequestsAsync(string? status, int take)
    {
        var items = _swapRequests
            .Where(item => string.IsNullOrWhiteSpace(status) || string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .Select(item => CloneSwapRequest(item))
            .ToArray();
        return Task.FromResult<IEnumerable<SwapRequest>>(items);
    }

    public Task<int> UpdateSwapRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? appliedGroupId)
    {
        var existing = _swapRequests.FirstOrDefault(item => item.Id == requestId);
        if (existing is null) return Task.FromResult(0);

        var index = _swapRequests.FindIndex(item => item.Id == requestId);
        _swapRequests[index] = CloneSwapRequest(existing, status, reviewedByEmail, reviewedByName, reviewedByRole, appliedGroupId);
        return Task.FromResult(1);
    }

    public Task<int> UpdateSwapRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? appliedGroupId, string? reviewComments)
    {
        var existing = _swapRequests.FirstOrDefault(item => item.Id == requestId);
        if (existing is null) return Task.FromResult(0);
        var index = _swapRequests.FindIndex(item => item.Id == requestId);
        _swapRequests[index] = CloneSwapRequest(existing, status, reviewedByEmail, reviewedByName, reviewedByRole, appliedGroupId, reviewComments);
        return Task.FromResult(1);
    }

    public Task<DateTime?> FindCoverageSnapshotWeekByGroupIdAsync(Guid groupId) => Task.FromResult<DateTime?>(null);
    public Task<WeeklyCoverageSnapshot?> GetCoverageSnapshotAsync(DateTime weekStartDate) => Task.FromResult<WeeklyCoverageSnapshot?>(null);
    public Task<int> SaveCoverageSnapshotAsync(WeeklyCoverageSnapshot snapshot) => Task.FromResult(1);
    public Task<int> UpsertCoverageSnapshotAsync(WeeklyCoverageSnapshot snapshot) => Task.FromResult(1);
    public Task<int> BackfillCoverageSnapshotItemsAsync(DateTime weekStartDate, string itemsJson) => Task.FromResult(1);

    private void ReplaceUser(User updated)
    {
        var index = _users.FindIndex(item => item.Id == updated.Id);
        if (index >= 0)
        {
            _users[index] = CloneUser(updated);
        }
    }

    private static User CloneUser(
        User user,
        Guid? objectId = null,
        string? displayName = null,
        int? role = null,
        bool? isActive = null,
        bool? isSystemHidden = null,
        string? passwordHash = null,
        bool? mustChangePassword = null,
        string? location = null,
        string? company = null,
        string? companyScope = null,
        string? operation = null,
        string? shiftTime = null,
        string? scheduleBlocks = null,
        IReadOnlyList<UserSchedulePeriod>? schedulePeriods = null)
    {
        return new User
        {
            Id = user.Id,
            TenantId = user.TenantId,
            ObjectId = objectId ?? user.ObjectId,
            Email = user.Email,
            DisplayName = displayName ?? user.DisplayName,
            Role = role ?? user.Role,
            IsActive = isActive ?? user.IsActive,
            IsSystemHidden = isSystemHidden ?? user.IsSystemHidden,
            PasswordHash = passwordHash ?? user.PasswordHash,
            CreatedAtUtc = user.CreatedAtUtc,
            MustChangePassword = mustChangePassword ?? user.MustChangePassword,
            Location = location ?? user.Location,
            Company = company ?? user.Company,
            CompanyScope = companyScope ?? user.CompanyScope,
            Operation = operation ?? user.Operation,
            ShiftTime = shiftTime ?? user.ShiftTime,
            ScheduleBlocks = scheduleBlocks ?? user.ScheduleBlocks,
            SchedulePeriods = schedulePeriods ?? user.SchedulePeriods.Select(ClonePeriod).ToArray()
        };
    }

    private static UserSchedulePeriod ClonePeriod(UserSchedulePeriod period) => new()
    {
        Id = period.Id,
        UserId = period.UserId,
        EffectiveFrom = period.EffectiveFrom,
        EffectiveTo = period.EffectiveTo,
        ShiftTime = period.ShiftTime,
        BlocksJson = period.BlocksJson,
        IsRepeating = period.IsRepeating,
        CreatedAtUtc = period.CreatedAtUtc
    };

    private static CompanyCatalogItem CloneCompany(CompanyCatalogItem company) => new()
    {
        Name = company.Name,
        IsActive = company.IsActive,
        CreatedAtUtc = company.CreatedAtUtc,
        UpdatedAtUtc = company.UpdatedAtUtc
    };

    private static CompanyOperationItem CloneCompanyOperation(CompanyOperationItem operation) => new()
    {
        CompanyName = operation.CompanyName,
        Name = operation.Name,
        IsActive = operation.IsActive,
        CreatedAtUtc = operation.CreatedAtUtc,
        UpdatedAtUtc = operation.UpdatedAtUtc
    };

    private static SwapRequest CloneSwapRequest(
        SwapRequest request,
        string? status = null,
        string? reviewedByEmail = null,
        string? reviewedByName = null,
        int? reviewedByRole = null,
        Guid? appliedGroupId = null,
        string? reviewComments = null) => new()
    {
        Id = request.Id,
        RequestedByUserId = request.RequestedByUserId,
        RequestedByEmail = request.RequestedByEmail,
        RequestedByDisplayName = request.RequestedByDisplayName,
        RequestedByRole = request.RequestedByRole,
        TargetUserId = request.TargetUserId,
        TargetUserEmail = request.TargetUserEmail,
        TargetUserDisplayName = request.TargetUserDisplayName,
        TargetUserRole = request.TargetUserRole,
        SwapDate = request.SwapDate,
        RequestedDatesJson = request.RequestedDatesJson,
        TargetDatesJson = request.TargetDatesJson,
        PairingsJson = request.PairingsJson,
        RequestType = request.RequestType,
        Comments = request.Comments,
        ReviewComments = reviewComments ?? request.ReviewComments,
        WeeklyHoursJson = request.WeeklyHoursJson,
        Status = status ?? request.Status,
        AppliedGroupId = appliedGroupId ?? request.AppliedGroupId,
        ReviewedByEmail = reviewedByEmail ?? request.ReviewedByEmail,
        ReviewedByName = reviewedByName ?? request.ReviewedByName,
        ReviewedByRole = reviewedByRole ?? request.ReviewedByRole,
        ReviewedAtUtc = reviewedByEmail is null ? request.ReviewedAtUtc : DateTime.UtcNow,
        CreatedAtUtc = request.CreatedAtUtc,
        UpdatedAtUtc = reviewedByEmail is null ? request.UpdatedAtUtc : DateTime.UtcNow
    };

    private static PtoRequest ClonePtoRequest(PtoRequest request) => new()
    {
        Id = request.Id,
        UserId = request.UserId,
        UserEmail = request.UserEmail,
        UserDisplayName = request.UserDisplayName,
        RequestType = request.RequestType,
        NumberOfDays = request.NumberOfDays,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        Comments = request.Comments,
        ReviewComments = request.ReviewComments,
        OverrideGroupId = request.OverrideGroupId,
        Status = request.Status,
        RequestedByEmail = request.RequestedByEmail,
        RequestedByName = request.RequestedByName,
        RequestedByRole = request.RequestedByRole,
        ReviewedByEmail = request.ReviewedByEmail,
        ReviewedByName = request.ReviewedByName,
        ReviewedByRole = request.ReviewedByRole,
        ReviewedAtUtc = request.ReviewedAtUtc,
        CreatedAtUtc = request.CreatedAtUtc,
        UpdatedAtUtc = request.UpdatedAtUtc
    };

    private static List<User> BuildDefaultUsers()
    {
        var activeLeadersId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var activeOutboundId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var inactiveId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        return
        [
            new User
            {
                Id = activeLeadersId,
                TenantId = Guid.NewGuid(),
                ObjectId = Guid.NewGuid(),
                Email = "jhon.doe@company.com",
                DisplayName = "Jhon Doe",
                Role = 1,
                IsActive = true,
                PasswordHash = "hash",
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow,
                Location = "COL",
                Company = "Solvo Global",
                Operation = "Leaders",
                ShiftTime = "Morning",
                ScheduleBlocks = BuildWeekdayScheduleJson(),
                SchedulePeriods = [BuildOpenPeriod(activeLeadersId, "Morning", BuildWeekdayScheduleJson())]
            },
            new User
            {
                Id = activeOutboundId,
                TenantId = Guid.NewGuid(),
                ObjectId = Guid.NewGuid(),
                Email = "jhon.smith@company.com",
                DisplayName = "Jhon Smith",
                Role = 0,
                IsActive = true,
                PasswordHash = "hash",
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow,
                Location = "COL",
                Company = "Solvo Global",
                Operation = "Outbound",
                ShiftTime = "Late",
                ScheduleBlocks = BuildWeekdayScheduleJson(),
                SchedulePeriods = [BuildOpenPeriod(activeOutboundId, "Late", BuildWeekdayScheduleJson())]
            },
            new User
            {
                Id = inactiveId,
                TenantId = Guid.NewGuid(),
                ObjectId = Guid.NewGuid(),
                Email = "inactive.user@company.com",
                DisplayName = "Inactive User",
                Role = 0,
                IsActive = false,
                PasswordHash = "hash",
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow,
                Location = "COL",
                Company = "Solvo Global",
                Operation = "Leaders",
                ShiftTime = "Morning",
                ScheduleBlocks = BuildWeekdayScheduleJson(),
                SchedulePeriods = [BuildOpenPeriod(inactiveId, "Morning", BuildWeekdayScheduleJson())]
            }
        ];
    }

    private static UserSchedulePeriod BuildOpenPeriod(Guid userId, string shiftTime, string blocksJson) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        EffectiveFrom = new DateTime(2026, 03, 01),
        EffectiveTo = null,
        ShiftTime = shiftTime,
        BlocksJson = blocksJson,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static string BuildWeekdayScheduleJson() =>
        JsonSerializer.Serialize(new[]
        {
            new { Start = "08:00", End = "17:00", Days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" } }
        });
}
