using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByObjectIdAsync(Guid objectId);
    Task<User?> GetByIdAsync(Guid id);
    Task<IEnumerable<User>> GetAllAsync();
    Task<IEnumerable<User>> GetInactiveAsync();
    Task<IEnumerable<User>> GetSystemHiddenAsync();
    Task<int> UpdatePasswordAsync(string email, string passwordHash, bool mustChangePassword = false);
    Task<int> SetSystemHiddenAsync(Guid id, bool isSystemHidden);
    Task<int> CountActiveSystemHiddenAsync();
    Task<bool> EmailExistsAsync(string email);
    Task<int> CreateUserAsync(User user);
    Task<int> UpdateObjectIdAsync(Guid id, Guid objectId);
    Task<int> UpdateUserAsync(Guid id, string displayName, int role, string location, string company, string? companyScope, string operation, string shiftTime, string? scheduleBlocks);
    Task<int> BulkUpsertUsersAsync(IEnumerable<BulkUserUpsert> users);
    Task<IEnumerable<CompanyCatalogItem>> GetCompaniesAsync(bool includeInactive);
    Task<int> UpsertCompanyAsync(string name, bool isActive);
    Task<int> SetCompanyActiveAsync(string name, bool isActive);
    Task<int> RenameCompanyAsync(string currentName, string newName);
    Task<IEnumerable<CompanyOperationItem>> GetCompanyOperationsAsync(string? companyName, bool includeInactive);
    Task<int> UpsertCompanyOperationAsync(string companyName, string name, bool isActive);
    Task<int> SetCompanyOperationActiveAsync(string companyName, string name, bool isActive);
    Task<int> RenameCompanyOperationAsync(string companyName, string currentName, string newName);
    Task<int> ReplaceUserSchedulePeriodsAsync(Guid userId, IEnumerable<UserSchedulePeriod> periods, string shiftTime, string? scheduleBlocks);
    Task<int> SoftDeleteAsync(Guid id);
    Task<int> ReactivateAsync(Guid id);
    Task<int> HardDeleteAsync(Guid id);
    Task<int> CreateScheduleEventAsync(ScheduleEvent scheduleEvent);
    Task<IEnumerable<ScheduleEvent>> GetRecentScheduleEventsAsync(int take);
    Task<IEnumerable<UserScheduleOverride>> GetScheduleOverridesAsync(DateTime fromDate, DateTime toDate);
    Task<int> RemoveScheduleOverridesByGroupAsync(Guid userId, Guid groupId);
    Task<IEnumerable<UserScheduleOverride>> GetScheduleOverridesByGroupAsync(Guid groupId);
    Task<Guid> ApplyPtoOverrideAsync(Guid userId, DateTime startDate, int numberOfDays, string requestType, string? comments, Guid? existingGroupId);
    Task<Guid> ApplyPtoOverrideDatesAsync(Guid userId, IEnumerable<DateTime> overrideDates, string requestType, string? comments, Guid? existingGroupId);
    Task<int> UpsertScheduleOverrideAsync(UserScheduleOverride scheduleOverride);
    Task<int> UpsertPtoRequestAsync(PtoRequest request);
    Task<PtoRequest?> GetPtoRequestAsync(Guid requestId);
    Task<PtoRequest?> GetLatestPtoRequestByGroupIdAsync(Guid groupId);
    Task<PtoRequest?> GetOverlappingActivePtoRequestAsync(Guid userId, DateTime startDate, DateTime endDate, Guid? excludingRequestOrGroupId);
    Task<IEnumerable<PtoRequest>> GetPtoRequestsAsync(string? status, int take);
    Task<int> UpdatePtoRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? overrideGroupId);
    Task<int> UpdatePtoRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? overrideGroupId, string? reviewComments);
    Task<int> CreateSwapRequestAsync(SwapRequest request);
    Task<SwapRequest?> GetSwapRequestAsync(Guid requestId);
    Task<IEnumerable<SwapRequest>> GetSwapRequestsAsync(string? status, int take);
    Task<int> UpdateSwapRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? appliedGroupId);
    Task<int> UpdateSwapRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? appliedGroupId, string? reviewComments);
    Task<DateTime?> FindCoverageSnapshotWeekByGroupIdAsync(Guid groupId);
    Task<WeeklyCoverageSnapshot?> GetCoverageSnapshotAsync(DateTime weekStartDate);
    Task<int> SaveCoverageSnapshotAsync(WeeklyCoverageSnapshot snapshot);
    Task<int> UpsertCoverageSnapshotAsync(WeeklyCoverageSnapshot snapshot);
    Task<int> BackfillCoverageSnapshotItemsAsync(DateTime weekStartDate, string itemsJson);
}

public record BulkUserUpsert(
    User User,
    IReadOnlyCollection<UserSchedulePeriod> SchedulePeriods,
    string LegacyShiftTime,
    string? LegacyScheduleBlocks,
    bool IsNewUser);
