using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;
using ShiftTrack.Infrastructure.Persistence;

namespace ShiftTrack.Infrastructure.Repositories;

public sealed class EfUserRepository(ShiftTrackDbContext dbContext) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Email == email);
        return await AttachSchedulePeriodsAsync(user);
    }

    public async Task<User?> GetByObjectIdAsync(Guid objectId)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.ObjectId == objectId);
        return await AttachSchedulePeriodsAsync(user);
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        return await AttachSchedulePeriodsAsync(user);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.IsActive && !item.IsSystemHidden)
            .ToArrayAsync();
        await AttachSchedulePeriodsAsync(users);
        return users;
    }

    public async Task<IEnumerable<User>> GetInactiveAsync()
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(item => !item.IsActive && !item.IsSystemHidden)
            .ToArrayAsync();
        await AttachSchedulePeriodsAsync(users);
        return users;
    }

    public async Task<IEnumerable<User>> GetSystemHiddenAsync()
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.IsActive && item.IsSystemHidden)
            .ToArrayAsync();
        await AttachSchedulePeriodsAsync(users);
        return users;
    }

    public Task<int> UpdatePasswordAsync(string email, string passwordHash, bool mustChangePassword = false) =>
        dbContext.Users
            .Where(item => item.Email == email && item.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PasswordHash, passwordHash)
                .SetProperty(item => item.MustChangePassword, mustChangePassword));

    public Task<int> SetSystemHiddenAsync(Guid id, bool isSystemHidden) =>
        dbContext.Users
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsSystemHidden, isSystemHidden));

    public Task<int> CountActiveSystemHiddenAsync() =>
        dbContext.Users.CountAsync(item => item.IsActive && item.IsSystemHidden);

    public Task<bool> EmailExistsAsync(string email) =>
        dbContext.Users.AnyAsync(item => item.Email == email);

    public async Task<int> CreateUserAsync(User user)
    {
        dbContext.Users.Add(user);
        return await dbContext.SaveChangesAsync();
    }

    public Task<int> UpdateObjectIdAsync(Guid id, Guid objectId) =>
        dbContext.Users
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ObjectId, objectId));

    public Task<int> UpdateUserAsync(Guid id, string displayName, int role, string location, string company, string? companyScope, string operation, string shiftTime, string? scheduleBlocks) =>
        dbContext.Users
            .Where(item => item.Id == id && item.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DisplayName, displayName)
                .SetProperty(item => item.Role, role)
                .SetProperty(item => item.Location, location)
                .SetProperty(item => item.Company, company)
                .SetProperty(item => item.CompanyScope, companyScope)
                .SetProperty(item => item.Operation, operation)
                .SetProperty(item => item.ShiftTime, shiftTime)
                .SetProperty(item => item.ScheduleBlocks, scheduleBlocks));

    public async Task<int> BulkUpsertUsersAsync(IEnumerable<BulkUserUpsert> users)
    {
        var items = users.ToArray();
        if (items.Length == 0) return 0;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        var affected = 0;

        foreach (var item in items)
        {
            if (item.IsNewUser)
            {
                dbContext.Users.Add(item.User);
                affected += await dbContext.SaveChangesAsync();
            }
            else
            {
                affected += await dbContext.Users
                    .Where(user => user.Id == item.User.Id && user.IsActive)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(user => user.DisplayName, item.User.DisplayName)
                        .SetProperty(user => user.Role, item.User.Role)
                        .SetProperty(user => user.Location, item.User.Location)
                        .SetProperty(user => user.Company, item.User.Company)
                        .SetProperty(user => user.CompanyScope, item.User.CompanyScope)
                        .SetProperty(user => user.Operation, item.User.Operation)
                        .SetProperty(user => user.ShiftTime, item.LegacyShiftTime)
                        .SetProperty(user => user.ScheduleBlocks, item.LegacyScheduleBlocks));
            }

            await dbContext.UserSchedulePeriods
                .Where(period => period.UserId == item.User.Id)
                .ExecuteDeleteAsync();

            dbContext.UserSchedulePeriods.AddRange(item.SchedulePeriods
                .OrderBy(period => period.EffectiveFrom)
                .Select(period => new UserSchedulePeriod
                {
                    Id = period.Id,
                    UserId = period.UserId,
                    EffectiveFrom = period.EffectiveFrom.Date,
                    EffectiveTo = period.EffectiveTo?.Date,
                    ShiftTime = period.ShiftTime,
                    BlocksJson = period.BlocksJson,
                    IsRepeating = period.IsRepeating,
                    CreatedAtUtc = period.CreatedAtUtc
                }));
            affected += await dbContext.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return affected;
    }

    public async Task<IEnumerable<CompanyCatalogItem>> GetCompaniesAsync(bool includeInactive) =>
        await dbContext.Companies
            .AsNoTracking()
            .Where(item => includeInactive || item.IsActive)
            .OrderBy(item => item.Name)
            .ToArrayAsync();

    public async Task<int> UpsertCompanyAsync(string name, bool isActive)
    {
        var updated = await dbContext.Companies
            .Where(item => item.Name == name)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsActive, isActive)
                .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow));

        if (updated > 0) return updated;

        dbContext.Companies.Add(new CompanyCatalogItem
        {
            Name = name,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        });
        return await dbContext.SaveChangesAsync();
    }

    public Task<int> SetCompanyActiveAsync(string name, bool isActive) =>
        UpsertCompanyAsync(name, isActive);

    public async Task<int> RenameCompanyAsync(string currentName, string newName)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync();
        var affected = 0;
        var now = DateTime.UtcNow;

        var currentExists = await dbContext.Companies.AnyAsync(item => item.Name == currentName);
        var newExists = await dbContext.Companies.AnyAsync(item => item.Name == newName);

        if (!currentExists && !newExists)
        {
            dbContext.Companies.Add(new CompanyCatalogItem { Name = newName, IsActive = true, CreatedAtUtc = now });
            affected += await dbContext.SaveChangesAsync();
        }
        else if (currentExists && newExists)
        {
            affected += await dbContext.Companies
                .Where(item => item.Name == newName)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.IsActive, true)
                    .SetProperty(item => item.UpdatedAtUtc, now));
            affected += await dbContext.Companies
                .Where(item => item.Name == currentName)
                .ExecuteDeleteAsync();
        }
        else if (currentExists)
        {
            affected += await dbContext.Companies
                .Where(item => item.Name == currentName)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Name, newName)
                    .SetProperty(item => item.UpdatedAtUtc, now));
        }

        affected += await dbContext.Users
            .Where(item => item.Company == currentName)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Company, newName));

        affected += await dbContext.CoverageRules
            .Where(item => item.CompanyName == currentName)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.CompanyName, newName)
                .SetProperty(item => item.UpdatedAtUtc, now));

        var sourceOperations = await dbContext.CompanyOperations
            .AsNoTracking()
            .Where(item => item.CompanyName == currentName)
            .ToArrayAsync();

        foreach (var source in sourceOperations)
        {
            if (!await dbContext.CompanyOperations.AnyAsync(target => target.CompanyName == newName && target.Name == source.Name))
            {
                dbContext.CompanyOperations.Add(new CompanyOperationItem
                {
                    CompanyName = newName,
                    Name = source.Name,
                    IsActive = source.IsActive,
                    CreatedAtUtc = now
                });
                affected += await dbContext.SaveChangesAsync();
            }
        }

        affected += await dbContext.CompanyOperations
            .Where(item => item.CompanyName == currentName)
            .ExecuteDeleteAsync();

        var scopedUsers = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.CompanyScope != null && item.CompanyScope.Contains(currentName))
            .Select(item => new { item.Id, item.CompanyScope })
            .ToArrayAsync();

        foreach (var row in scopedUsers)
        {
            var updatedScope = ReplaceCompanyInScope(row.CompanyScope, currentName, newName);
            if (updatedScope is null || updatedScope == row.CompanyScope) continue;

            affected += await dbContext.Users
                .Where(item => item.Id == row.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CompanyScope, updatedScope));
        }

        await tx.CommitAsync();
        return affected;
    }

    public async Task<IEnumerable<CompanyOperationItem>> GetCompanyOperationsAsync(string? companyName, bool includeInactive) =>
        await dbContext.CompanyOperations
            .AsNoTracking()
            .Where(item => companyName == null || item.CompanyName == companyName)
            .Where(item => includeInactive || item.IsActive)
            .OrderBy(item => item.CompanyName)
            .ThenBy(item => item.Name)
            .ToArrayAsync();

    public async Task<int> UpsertCompanyOperationAsync(string companyName, string name, bool isActive)
    {
        var updated = await dbContext.CompanyOperations
            .Where(item => item.CompanyName == companyName && item.Name == name)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsActive, isActive)
                .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow));

        if (updated > 0) return updated;

        dbContext.CompanyOperations.Add(new CompanyOperationItem
        {
            CompanyName = companyName,
            Name = name,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        });
        return await dbContext.SaveChangesAsync();
    }

    public Task<int> SetCompanyOperationActiveAsync(string companyName, string name, bool isActive) =>
        UpsertCompanyOperationAsync(companyName, name, isActive);

    public async Task<int> RenameCompanyOperationAsync(string companyName, string currentName, string newName)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync();
        var affected = 0;
        var now = DateTime.UtcNow;

        if (await dbContext.CompanyOperations.AnyAsync(item => item.CompanyName == companyName && item.Name == newName))
        {
            affected += await dbContext.CompanyOperations
                .Where(item => item.CompanyName == companyName && item.Name == newName)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.IsActive, true)
                    .SetProperty(item => item.UpdatedAtUtc, now));

            affected += await dbContext.CompanyOperations
                .Where(item => item.CompanyName == companyName && item.Name == currentName)
                .ExecuteDeleteAsync();
        }
        else
        {
            affected += await dbContext.CompanyOperations
                .Where(item => item.CompanyName == companyName && item.Name == currentName)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Name, newName)
                    .SetProperty(item => item.UpdatedAtUtc, now));
        }

        affected += await dbContext.Users
            .Where(item => item.Company == companyName && item.Operation == currentName)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Operation, newName));

        affected += await dbContext.CoverageRules
            .Where(item => item.CompanyName == companyName && item.OperationName == currentName)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.OperationName, newName)
                .SetProperty(item => item.UpdatedAtUtc, now));

        await tx.CommitAsync();
        return affected;
    }

    public async Task<int> ReplaceUserSchedulePeriodsAsync(Guid userId, IEnumerable<UserSchedulePeriod> periods, string shiftTime, string? scheduleBlocks)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync();

        await dbContext.UserSchedulePeriods
            .Where(item => item.UserId == userId)
            .ExecuteDeleteAsync();

        dbContext.UserSchedulePeriods.AddRange(periods
            .OrderBy(item => item.EffectiveFrom)
            .Select(period => new UserSchedulePeriod
            {
                Id = period.Id,
                UserId = period.UserId,
                EffectiveFrom = period.EffectiveFrom.Date,
                EffectiveTo = period.EffectiveTo?.Date,
                ShiftTime = period.ShiftTime,
                BlocksJson = period.BlocksJson,
                IsRepeating = period.IsRepeating,
                CreatedAtUtc = period.CreatedAtUtc
            }));
        await dbContext.SaveChangesAsync();

        var rows = await dbContext.Users
            .Where(item => item.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ShiftTime, shiftTime)
                .SetProperty(item => item.ScheduleBlocks, scheduleBlocks));

        await tx.CommitAsync();
        return rows;
    }

    public Task<int> SoftDeleteAsync(Guid id) =>
        dbContext.Users.Where(item => item.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false));

    public Task<int> ReactivateAsync(Guid id) =>
        dbContext.Users.Where(item => item.Id == id && !item.IsActive).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, true));

    public Task<int> HardDeleteAsync(Guid id) =>
        dbContext.Users.Where(item => item.Id == id).ExecuteDeleteAsync();

    public async Task<int> CreateScheduleEventAsync(ScheduleEvent scheduleEvent)
    {
        dbContext.ScheduleEvents.Add(scheduleEvent);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<ScheduleEvent>> GetRecentScheduleEventsAsync(int take) =>
        await dbContext.ScheduleEvents
            .AsNoTracking()
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .ToArrayAsync();

    public async Task<IEnumerable<UserScheduleOverride>> GetScheduleOverridesAsync(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        return await dbContext.UserScheduleOverrides
            .AsNoTracking()
            .Where(item => item.OverrideDate >= from && item.OverrideDate <= to)
            .ToArrayAsync();
    }

    public async Task<int> RemoveScheduleOverridesByGroupAsync(Guid userId, Guid groupId) =>
        await dbContext.UserScheduleOverrides
            .Where(item => item.UserId == userId && item.GroupId == groupId)
            .ExecuteDeleteAsync();

    public async Task<IEnumerable<UserScheduleOverride>> GetScheduleOverridesByGroupAsync(Guid groupId) =>
        await dbContext.UserScheduleOverrides
            .AsNoTracking()
            .Where(item => item.GroupId == groupId)
            .OrderBy(item => item.OverrideDate)
            .ToArrayAsync();

    public async Task<Guid> ApplyPtoOverrideAsync(Guid userId, DateTime startDate, int numberOfDays, string requestType, string? comments, Guid? existingGroupId)
    {
        if (numberOfDays < 1) throw new ArgumentOutOfRangeException(nameof(numberOfDays), "Number of days must be at least 1.");
        var dates = Enumerable.Range(0, numberOfDays).Select(i => startDate.Date.AddDays(i));
        return await ApplyPtoOverrideDatesAsync(userId, dates, requestType, comments, existingGroupId);
    }

    public async Task<Guid> ApplyPtoOverrideDatesAsync(Guid userId, IEnumerable<DateTime> overrideDates, string requestType, string? comments, Guid? existingGroupId)
    {
        var dates = overrideDates.Select(item => item.Date).Distinct().OrderBy(item => item).ToArray();
        if (dates.Length == 0) throw new ArgumentOutOfRangeException(nameof(overrideDates), "At least one PTO override date is required.");

        var normalizedRequestType = string.IsNullOrWhiteSpace(requestType)
            ? "pto"
            : requestType.Trim().ToLowerInvariant();
        var sanitizedComments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();

        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var effectiveGroupId = existingGroupId;
        if (!effectiveGroupId.HasValue)
        {
            var ptoTypes = new[]
            {
                "pto", "dayoff", "day_off", "vacations", "absence", "sickleave", "sick_leave",
                "maternityleave", "maternity_leave", "paternityleave", "paternity_leave"
            };

            effectiveGroupId = await dbContext.UserScheduleOverrides
                .Where(item => item.UserId == userId &&
                               item.OverrideDate == dates[0] &&
                               ptoTypes.Contains(item.EntryType) &&
                               item.GroupId != null)
                .Select(item => item.GroupId)
                .FirstOrDefaultAsync();
        }

        if (effectiveGroupId.HasValue)
        {
            await dbContext.UserScheduleOverrides
                .Where(item => item.UserId == userId && item.GroupId == effectiveGroupId.Value)
                .ExecuteDeleteAsync();
        }

        var newGroupId = existingGroupId ?? Guid.NewGuid();

        foreach (var day in dates)
        {
            await dbContext.UserScheduleOverrides
                .Where(item => item.UserId == userId && item.OverrideDate == day)
                .ExecuteDeleteAsync();

            dbContext.UserScheduleOverrides.Add(new UserScheduleOverride
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OverrideDate = day,
                GroupId = newGroupId,
                EntryType = normalizedRequestType,
                RequestType = normalizedRequestType,
                Comments = sanitizedComments,
                Label = normalizedRequestType is "dayoff" or "day_off" ? "Day Off" : "PTO",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();
        return newGroupId;
    }

    public async Task<int> UpsertScheduleOverrideAsync(UserScheduleOverride scheduleOverride)
    {
        var updated = await dbContext.UserScheduleOverrides
            .Where(item => item.UserId == scheduleOverride.UserId && item.OverrideDate == scheduleOverride.OverrideDate.Date)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.GroupId, scheduleOverride.GroupId)
                .SetProperty(item => item.EntryType, scheduleOverride.EntryType)
                .SetProperty(item => item.RequestType, scheduleOverride.RequestType)
                .SetProperty(item => item.Comments, scheduleOverride.Comments)
                .SetProperty(item => item.StartTime, scheduleOverride.StartTime)
                .SetProperty(item => item.EndTime, scheduleOverride.EndTime)
                .SetProperty(item => item.Label, scheduleOverride.Label)
                .SetProperty(item => item.CreatedAtUtc, scheduleOverride.CreatedAtUtc));

        if (updated > 0) return updated;

        dbContext.UserScheduleOverrides.Add(new UserScheduleOverride
        {
            Id = scheduleOverride.Id,
            UserId = scheduleOverride.UserId,
            OverrideDate = scheduleOverride.OverrideDate.Date,
            GroupId = scheduleOverride.GroupId,
            EntryType = scheduleOverride.EntryType,
            RequestType = scheduleOverride.RequestType,
            Comments = scheduleOverride.Comments,
            StartTime = scheduleOverride.StartTime,
            EndTime = scheduleOverride.EndTime,
            Label = scheduleOverride.Label,
            CreatedAtUtc = scheduleOverride.CreatedAtUtc
        });
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> UpsertPtoRequestAsync(PtoRequest request)
    {
        var updated = await dbContext.PtoRequests
            .Where(item => item.Id == request.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UserId, request.UserId)
                .SetProperty(item => item.UserEmail, request.UserEmail)
                .SetProperty(item => item.UserDisplayName, request.UserDisplayName)
                .SetProperty(item => item.RequestType, request.RequestType)
                .SetProperty(item => item.NumberOfDays, request.NumberOfDays)
                .SetProperty(item => item.StartDate, request.StartDate.Date)
                .SetProperty(item => item.EndDate, request.EndDate.Date)
                .SetProperty(item => item.Comments, request.Comments)
                .SetProperty(item => item.OverrideGroupId, request.OverrideGroupId)
                .SetProperty(item => item.Status, request.Status)
                .SetProperty(item => item.RequestedByEmail, request.RequestedByEmail)
                .SetProperty(item => item.RequestedByName, request.RequestedByName)
                .SetProperty(item => item.RequestedByRole, request.RequestedByRole)
                .SetProperty(item => item.UpdatedAtUtc, request.UpdatedAtUtc));

        if (updated > 0) return updated;

        dbContext.PtoRequests.Add(new PtoRequest
        {
            Id = request.Id,
            UserId = request.UserId,
            UserEmail = request.UserEmail,
            UserDisplayName = request.UserDisplayName,
            RequestType = request.RequestType,
            NumberOfDays = request.NumberOfDays,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
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
        });
        return await dbContext.SaveChangesAsync();
    }

    public Task<PtoRequest?> GetPtoRequestAsync(Guid requestId) =>
        dbContext.PtoRequests.AsNoTracking().FirstOrDefaultAsync(item => item.Id == requestId);

    public Task<PtoRequest?> GetLatestPtoRequestByGroupIdAsync(Guid groupId) =>
        dbContext.PtoRequests
            .AsNoTracking()
            .Where(item => item.OverrideGroupId == groupId || item.Id == groupId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync();

    public Task<PtoRequest?> GetOverlappingActivePtoRequestAsync(Guid userId, DateTime startDate, DateTime endDate, Guid? excludingRequestOrGroupId)
    {
        var from = startDate.Date;
        var to = endDate.Date;

        return dbContext.PtoRequests
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           (item.Status == "pending" || item.Status == "approved") &&
                           item.StartDate <= to &&
                           item.EndDate >= from)
            .Where(item => excludingRequestOrGroupId == null ||
                           (item.Id != excludingRequestOrGroupId &&
                            (item.OverrideGroupId == null || item.OverrideGroupId != excludingRequestOrGroupId)))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PtoRequest>> GetPtoRequestsAsync(string? status, int take)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();

        return await dbContext.PtoRequests
            .AsNoTracking()
            .Where(item => normalizedStatus == null || item.Status == normalizedStatus)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToArrayAsync();
    }

    public Task<int> UpdatePtoRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? overrideGroupId) =>
        UpdatePtoRequestStatusAsync(requestId, status, reviewedByEmail, reviewedByName, reviewedByRole, overrideGroupId, null);

    public Task<int> UpdatePtoRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? overrideGroupId, string? reviewComments) =>
        dbContext.PtoRequests
            .Where(item => item.Id == requestId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.OverrideGroupId, item => overrideGroupId ?? item.OverrideGroupId)
                .SetProperty(item => item.ReviewedByEmail, reviewedByEmail)
                .SetProperty(item => item.ReviewedByName, reviewedByName)
                .SetProperty(item => item.ReviewedByRole, reviewedByRole)
                .SetProperty(item => item.ReviewComments, item => reviewComments ?? item.ReviewComments)
                .SetProperty(item => item.ReviewedAtUtc, DateTime.UtcNow)
                .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow));

    public async Task<int> CreateSwapRequestAsync(SwapRequest request)
    {
        dbContext.SwapRequests.Add(request);
        return await dbContext.SaveChangesAsync();
    }

    public Task<SwapRequest?> GetSwapRequestAsync(Guid requestId) =>
        dbContext.SwapRequests.AsNoTracking().FirstOrDefaultAsync(item => item.Id == requestId);

    public async Task<IEnumerable<SwapRequest>> GetSwapRequestsAsync(string? status, int take)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();

        return await dbContext.SwapRequests
            .AsNoTracking()
            .Where(item => normalizedStatus == null || item.Status == normalizedStatus)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToArrayAsync();
    }

    public Task<int> UpdateSwapRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? appliedGroupId) =>
        UpdateSwapRequestStatusAsync(requestId, status, reviewedByEmail, reviewedByName, reviewedByRole, appliedGroupId, null);

    public Task<int> UpdateSwapRequestStatusAsync(Guid requestId, string status, string reviewedByEmail, string reviewedByName, int reviewedByRole, Guid? appliedGroupId, string? reviewComments) =>
        dbContext.SwapRequests
            .Where(item => item.Id == requestId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.AppliedGroupId, item => appliedGroupId ?? item.AppliedGroupId)
                .SetProperty(item => item.ReviewedByEmail, reviewedByEmail)
                .SetProperty(item => item.ReviewedByName, reviewedByName)
                .SetProperty(item => item.ReviewedByRole, reviewedByRole)
                .SetProperty(item => item.ReviewComments, item => reviewComments ?? item.ReviewComments)
                .SetProperty(item => item.ReviewedAtUtc, DateTime.UtcNow)
                .SetProperty(item => item.UpdatedAtUtc, DateTime.UtcNow));

    public Task<DateTime?> FindCoverageSnapshotWeekByGroupIdAsync(Guid groupId) =>
        dbContext.WeeklyCoverageSnapshots
            .AsNoTracking()
            .Where(item => item.ItemsJson != null && item.ItemsJson.Contains(groupId.ToString("D")))
            .OrderByDescending(item => item.WeekStartDate)
            .Select(item => (DateTime?)item.WeekStartDate)
            .FirstOrDefaultAsync();

    public Task<WeeklyCoverageSnapshot?> GetCoverageSnapshotAsync(DateTime weekStartDate) =>
        dbContext.WeeklyCoverageSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.WeekStartDate == weekStartDate.Date);

    public async Task<int> SaveCoverageSnapshotAsync(WeeklyCoverageSnapshot snapshot)
    {
        if (await dbContext.WeeklyCoverageSnapshots.AnyAsync(item => item.WeekStartDate == snapshot.WeekStartDate.Date))
        {
            return 0;
        }

        dbContext.WeeklyCoverageSnapshots.Add(new WeeklyCoverageSnapshot
        {
            WeekStartDate = snapshot.WeekStartDate.Date,
            PayloadJson = snapshot.PayloadJson,
            ItemsJson = snapshot.ItemsJson,
            CreatedAtUtc = snapshot.CreatedAtUtc
        });
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> UpsertCoverageSnapshotAsync(WeeklyCoverageSnapshot snapshot)
    {
        var updated = await dbContext.WeeklyCoverageSnapshots
            .Where(item => item.WeekStartDate == snapshot.WeekStartDate.Date)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PayloadJson, snapshot.PayloadJson)
                .SetProperty(item => item.ItemsJson, snapshot.ItemsJson)
                .SetProperty(item => item.CreatedAtUtc, snapshot.CreatedAtUtc));

        if (updated > 0) return updated;

        dbContext.WeeklyCoverageSnapshots.Add(new WeeklyCoverageSnapshot
        {
            WeekStartDate = snapshot.WeekStartDate.Date,
            PayloadJson = snapshot.PayloadJson,
            ItemsJson = snapshot.ItemsJson,
            CreatedAtUtc = snapshot.CreatedAtUtc
        });
        return await dbContext.SaveChangesAsync();
    }

    public Task<int> BackfillCoverageSnapshotItemsAsync(DateTime weekStartDate, string itemsJson) =>
        dbContext.WeeklyCoverageSnapshots
            .Where(item => item.WeekStartDate == weekStartDate.Date &&
                           (item.ItemsJson == null || item.ItemsJson.Trim() == string.Empty))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ItemsJson, itemsJson));

    private async Task<User?> AttachSchedulePeriodsAsync(User? user)
    {
        if (user is null) return null;
        await AttachSchedulePeriodsAsync(new[] { user });
        return user;
    }

    private async Task AttachSchedulePeriodsAsync(IReadOnlyCollection<User> users)
    {
        if (users.Count == 0) return;

        var userIds = users.Select(user => user.Id).Distinct().ToArray();
        var periods = await dbContext.UserSchedulePeriods
            .AsNoTracking()
            .Where(period => userIds.Contains(period.UserId))
            .OrderBy(period => period.EffectiveFrom)
            .ThenBy(period => period.CreatedAtUtc)
            .ToArrayAsync();

        var map = periods
            .GroupBy(period => period.UserId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<UserSchedulePeriod>)group.ToArray());

        foreach (var user in users)
        {
            user.SchedulePeriods = map.TryGetValue(user.Id, out var userPeriods)
                ? userPeriods
                : Array.Empty<UserSchedulePeriod>();
        }
    }

    private static string? ReplaceCompanyInScope(string? companyScope, string currentName, string newName)
    {
        if (string.IsNullOrWhiteSpace(companyScope)) return companyScope;

        string[] companies;
        try
        {
            companies = JsonSerializer.Deserialize<string[]>(companyScope) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            companies = companyScope.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var updated = companies
            .Select(company => string.Equals(company.Trim(), currentName, StringComparison.OrdinalIgnoreCase) ? newName : company.Trim())
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(updated);
    }
}
